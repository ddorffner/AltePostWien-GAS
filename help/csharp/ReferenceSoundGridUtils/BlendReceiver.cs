using System;
using System.Collections.Generic;
using System.Linq;
// Assuming YourOscLibrary is accessible, e.g., if BlendSender.cs (where it's defined) is in the same project.
// If YourOscLibrary were in a separate assembly, you'd add a using statement for that namespace directly.
using YourOscLibrary; 

namespace Main
{
    public class BlendReceiver : IDisposable
    {
        private readonly IOscReceiver _oscReceiver;
        private readonly string _targetMessageTitlePrefix;
        private readonly Dictionary<string, SortedDictionary<int, List<float>>> _incomingMessageChunks;
        private readonly object _lock = new object(); // For thread safety
        private bool _isDisposed = false;

        /// <summary>
        /// Initializes a new instance of the BlendReceiver class.
        /// </summary>
        /// <param name="port">The port to listen for OSC messages on.</param>
        /// <param name="targetMessageTitlePrefix">Optional. If specified, only OSC messages with addresses starting with this prefix (e.g., "/blendshapes") will be processed for chunking. 
        /// The full address is expected to be prefix/chunkIndex, e.g. /blendshapes/0, /blendshapes/1 etc.
        /// If null or empty, it will attempt to process any message that has a numerical last part in its address as chunk index.</param>
        public BlendReceiver(int port, string targetMessageTitlePrefix = null)
        {
            _oscReceiver = new OscReceiver(port);
            _targetMessageTitlePrefix = targetMessageTitlePrefix?.TrimEnd('/'); // Ensure no trailing slash
            _incomingMessageChunks = new Dictionary<string, SortedDictionary<int, List<float>>>();

            _oscReceiver.MessageReceived += OnOscMessageReceived;
            try
            {
                _oscReceiver.StartListening();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"BlendReceiver: Failed to start OSC listener on port {port}: {ex.Message}");
                // Consider how to handle this - BlendReceiver might be unusable.
                // For now, it will just not receive messages.
            }
        }

        private void OnOscMessageReceived(IOscMessage message)
        {
            if (_isDisposed) return;

            string address = message.Address;
            string baseTitle;
            int chunkIndex;

            int lastSlash = address.LastIndexOf('/');
            if (lastSlash == -1 || lastSlash == address.Length - 1) return; // No chunk index part

            string indexPart = address.Substring(lastSlash + 1);
            if (!int.TryParse(indexPart, out chunkIndex) || chunkIndex < 0) return; // Not a valid chunk index

            baseTitle = address.Substring(0, lastSlash);

            // Filter by target message title prefix if one is set
            if (!string.IsNullOrEmpty(_targetMessageTitlePrefix) && !baseTitle.Equals(_targetMessageTitlePrefix))
            {
                 // Console.WriteLine($"BlendReceiver: Ignoring message for {baseTitle}, expecting prefix {_targetMessageTitlePrefix}");
                return;
            }
            
            // All arguments should be floats for messages from BlendSender
            List<float> floatArgs = new List<float>();
            bool allFloats = true;
            if (message.Arguments != null)
            {
                foreach (var arg in message.Arguments)
                {
                    if (arg is float f)
                    {
                        floatArgs.Add(f);
                    }
                    else
                    {
                        // Console.WriteLine($"BlendReceiver: Message {address} contained non-float argument: {arg?.GetType().Name ?? "null"}. Discarding message.");
                        allFloats = false;
                        break;
                    }
                }
            }

            if (!allFloats || floatArgs.Count == 0 && message.Arguments != null && message.Arguments.Length > 0) // Allow empty argument list for potentially empty chunks if that's a use case
            {
                // If not all arguments are floats (and there were arguments), this message is not what we expect from BlendSender for float lists.
                // Or, if there were arguments but none were parsed to floats (e.g. all null or wrong type)
                return;
            }

            lock (_lock)
            {
                if (!_incomingMessageChunks.TryGetValue(baseTitle, out SortedDictionary<int, List<float>> chunks))
                {
                    chunks = new SortedDictionary<int, List<float>>();
                    _incomingMessageChunks[baseTitle] = chunks;
                }

                // If we receive chunk 0 for a title we already have chunks for, 
                // it implies a new transmission of that message has started.
                // Clear old chunks for this title to avoid mixing old and new data.
                if (chunkIndex == 0 && chunks.Count > 0) 
                {
                    Console.WriteLine($"BlendReceiver: Detected new transmission (chunk 0) for '{baseTitle}'. Clearing previous chunks.");
                    chunks.Clear();
                }
                chunks[chunkIndex] = floatArgs;
                // Console.WriteLine($"BlendReceiver: Received chunk {chunkIndex} for '{baseTitle}" with {floatArgs.Count} floats.");
            }
        }

        /// <summary>
        /// Attempts to retrieve a complete list of floats for a given message title.
        /// A message is considered complete if chunks from index 0 up to the highest received index for that title are present without gaps.
        /// If a complete message is retrieved, its stored chunks are cleared to make way for a new transmission.
        /// </summary>
        /// <param name="messageTitle">The base message title (e.g., "/blendshapes") for which to retrieve the complete data.</param>
        /// <returns>A List<float> containing all reassembled floats if the message is complete, otherwise null.</returns>
        public List<float> GetCompleteMessage(string messageTitle)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(BlendReceiver));
            if (string.IsNullOrEmpty(messageTitle)) return null;

            string cleanedMessageTitle = messageTitle.TrimEnd('/');

            lock (_lock)
            {
                if (_incomingMessageChunks.TryGetValue(cleanedMessageTitle, out SortedDictionary<int, List<float>> chunks))
                {
                    if (chunks.Count == 0 || chunks.First().Key != 0) // Must start with chunk 0
                    {
                        return null; 
                    }

                    List<float> completeList = new List<float>();
                    int expectedChunkIndex = 0;
                    foreach (var pair in chunks)
                    {
                        if (pair.Key != expectedChunkIndex)
                        {
                            // Gap detected, message not yet complete
                            return null; 
                        }
                        completeList.AddRange(pair.Value);
                        expectedChunkIndex++;
                    }

                    // If we reached here, all chunks from 0 to N were present without gaps.
                    // Message is considered complete.
                    _incomingMessageChunks.Remove(cleanedMessageTitle); // Clear stored chunks for this message title
                    Console.WriteLine($"BlendReceiver: Reassembled complete message for '{cleanedMessageTitle}' with {completeList.Count} floats.");
                    return completeList;
                }
                return null; // No chunks found for this title
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
            {
                lock(_lock) // Ensure thread safety during disposal
                {
                    if (_oscReceiver != null)
                    {
                        _oscReceiver.MessageReceived -= OnOscMessageReceived;
                        _oscReceiver.Dispose(); // This will stop listening and dispose the UdpClient
                    }
                    _incomingMessageChunks.Clear();
                }
            }
            _isDisposed = true;
            Console.WriteLine("BlendReceiver disposed.");
        }
    }
} 