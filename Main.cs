using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO.MemoryMappedFiles;
using Newtonsoft.Json;
using Plugfy.Core.Commons.Runtime; // Assumes that the IDataCommunication interface and DataReceivedEventArgs are defined in this namespace

namespace Plugfy.Core.Communication
{
    /// <summary>
    /// Communication extension using shared memory (Shared Memory).
    /// 
    /// Protocol:
    /// - The memory area is structured as follows:
    ///   - The first 4 bytes (int32) store the length of the message (if zero, no message exists).
    ///   - Starting at byte 4, the message content (JSON encoded in UTF8) is written.
    /// - For writing, the mutex is acquired, the length and the content are written, and then the event is signaled.
    /// - For reading, the listening task waits for the event, acquires the mutex, reads the length and the content, and resets the length to zero.
    /// - The read message is deserialized into a DataReceivedEventArgs object and the DataReceived event is triggered.
    /// 
    /// Initialization parameters must be provided in JSON format (or as a dynamic object) containing at least:
    /// - MapName: Unique name for the memory mapped file.
    /// - MapSize: (optional) Size in bytes of the buffer (default is 4096).
    /// </summary>
    public class SharedMemory : IDataCommunication
    {
        public string Name => "SharedMemory";
        public string Description => "Communication using shared memory (Shared Memory).";

        public event EventHandler<DataReceivedEventArgs> DataReceived;

        private MemoryMappedFile _mmf;
        private MemoryMappedViewAccessor _accessor;
        private Mutex _mutex;
        private EventWaitHandle _dataAvailableEvent;
        private CancellationTokenSource _cts;
        private Task _listeningTask;
        private bool _isClosed;

        // Name and size of the memory mapped file (configurable via parameters)
        private string _mapName;
        private int _mapSize;

        public bool IsClosed => _isClosed;

        /// <summary>
        /// Initializes the shared memory communication.
        /// Parameters must be a JSON string or dynamic object containing at least:
        /// {
        ///    "MapName": "UniqueMemoryMappedFileName",
        ///    "MapSize": 4096   // optional
        /// }
        /// </summary>
        /// <param name="parameters">Initialization parameters</param>
        public async Task InitializeAsync(dynamic parameters)
        {
            // If parameters is a string, deserialize it as JSON.
            if (parameters is string)
            {
                parameters = JsonConvert.DeserializeObject(parameters.ToString());
            }
            if (parameters == null || parameters.MapName == null)
            {
                throw new ArgumentException("Parameter 'MapName' is required for SharedMemory.");
            }
            _mapName = parameters.MapName;
            _mapSize = parameters.MapSize != null ? (int)parameters.MapSize : 4096;

            // Create or open the memory mapped file with read/write access.
            _mmf = MemoryMappedFile.CreateOrOpen(_mapName, _mapSize, MemoryMappedFileAccess.ReadWrite);
            _accessor = _mmf.CreateViewAccessor();

            // Create or open the mutex and event wait handle with names based on _mapName.
            string mutexName = "Global\\" + _mapName + "_Mutex";
            _mutex = new Mutex(false, mutexName);

            string eventName = "Global\\" + _mapName + "_Event";
            bool createdNew;
            _dataAvailableEvent = new EventWaitHandle(false, EventResetMode.AutoReset, eventName, out createdNew);

            _cts = new CancellationTokenSource();
            _isClosed = false;

            await Task.CompletedTask;
        }

        /// <summary>
        /// Sends data by writing the serialized JSON to shared memory.
        /// If the message exceeds the buffer size, an exception is thrown.
        /// </summary>
        /// <param name="data">Dynamic object to be sent</param>
        public async Task SendDataAsync(dynamic data)
        {
            if (_accessor == null)
                throw new InvalidOperationException("Shared memory is not initialized.");

            string json = JsonConvert.SerializeObject(data);
            byte[] bytes = Encoding.UTF8.GetBytes(json);

            // Check if the message fits in the buffer (considering 4 bytes for the length).
            if (bytes.Length + 4 > _mapSize)
                throw new InvalidOperationException("Message too large for the shared memory buffer.");

            _mutex.WaitOne();
            try
            {
                // Write the message length (first 4 bytes) and then the content.
                _accessor.Write(0, bytes.Length);
                _accessor.WriteArray(4, bytes, 0, bytes.Length);
            }
            finally
            {
                _mutex.ReleaseMutex();
            }

            // Signal that new data is available.
            _dataAvailableEvent.Set();

            await Task.CompletedTask;
        }

        /// <summary>
        /// Closes the communication by releasing resources (cancelling the listening task, closing the memory mapped file, etc.).
        /// </summary>
        public async Task CloseAsync()
        {
            _isClosed = true;
            _cts.Cancel();

            if (_listeningTask != null)
            {
                try
                {
                    await _listeningTask;
                }
                catch (OperationCanceledException) { }
            }

            _accessor?.Dispose();
            _mmf?.Dispose();
            _mutex?.Dispose();
            _dataAvailableEvent?.Dispose();
            _cts.Dispose();

            await Task.CompletedTask;
        }

        /// <summary>
        /// Starts the listening task to receive data via shared memory.
        /// As soon as the "data available" event is signaled, the message is read and processed.
        /// </summary>
        public async Task StartListeningAsync()
        {
            _listeningTask = Task.Run(() => ListeningLoop(_cts.Token));
            await Task.CompletedTask;
        }

        /// <summary>
        /// Listening loop that waits for the data available event, reads the data from memory, and triggers the DataReceived event.
        /// </summary>
        /// <param name="token">Cancellation token</param>
        private void ListeningLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && !_isClosed)
            {
                // Wait up to 100ms for the event (allows periodic cancellation checking)
                if (_dataAvailableEvent.WaitOne(100))
                {
                    if (token.IsCancellationRequested)
                        break;

                    string message = null;
                    _mutex.WaitOne();
                    try
                    {
                        int length = _accessor.ReadInt32(0);
                        // If there is a message (length > 0 and within the buffer limit)
                        if (length > 0 && length < _mapSize - 4)
                        {
                            byte[] buffer = new byte[length];
                            _accessor.ReadArray(4, buffer, 0, length);
                            message = Encoding.UTF8.GetString(buffer);
                            // Reset the length to 0 to indicate the message has been consumed.
                            _accessor.Write(0, 0);
                        }
                    }
                    finally
                    {
                        _mutex.ReleaseMutex();
                    }

                    if (!string.IsNullOrEmpty(message))
                    {
                        try
                        {
                            // Deserialize the message and trigger the DataReceived event.
                            DataReceivedEventArgs dataEvent = JsonConvert.DeserializeObject<DataReceivedEventArgs>(message);
                            DataReceived?.Invoke(this, dataEvent);
                        }
                        catch (JsonException ex)
                        {
                            Console.Error.WriteLine($"Error processing JSON message in SharedMemory: {ex.Message}");
                        }
                    }
                }
            }
        }
    }
}
