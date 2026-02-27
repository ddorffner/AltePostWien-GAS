using System;
using System.Collections.Generic;
using System.Linq; // For Linq operations if needed, like .Cast<object>().ToArray()
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
using YourOscLibrary; // Ensure this is present

// TODO: Replace 'YourOscLibrary' with the actual OSC library you are using.
// For example, if using Rug.Osc, you might have:
// using Rug.Osc;
// Or for OscCore:
// using OscCore;

namespace Main
{
    public class BlendSender : IDisposable
    {
        private IOscSender _oscSender;
        private bool _isDisposed = false;

        /// <summary>
        /// Initializes a new instance of the BlendSender class and establishes an OSC connection.
        /// </summary>
        /// <param name="ipAddress">The IP address of the OSC server.</param>
        /// <param name="port">The port number of the OSC server.</param>
        public BlendSender(string ipAddress, int port)
        {
            _oscSender = new OscSender(ipAddress, port);
            try
            {
                _oscSender.Connect(); 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing or connecting OSC sender: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends a list of float values as one or more OSC messages.
        /// The list is split into chunks if it exceeds maxFloatsPerMessage.
        /// </summary>
        /// <param name="data">The list of float values to send.</param>
        /// <param name="messageTitle">The base OSC address (e.g., "/blendshapes"). Chunk index will be appended.</param>
        /// <param name="maxFloatsPerMessage">The maximum number of floats to include in a single OSC message.</param>
        public void SendBlend(List<float> data, string messageTitle, int maxFloatsPerMessage)
        {
            if (!_isDisposed)
            {
                if (data == null || data.Count == 0)
                {
                    // Console.WriteLine("BlendSender: No data to send.");
                    return;
                }

                if (string.IsNullOrEmpty(messageTitle))
                {
                    // Console.WriteLine("BlendSender: Message title cannot be null or empty.");
                    return;
                }

                if (maxFloatsPerMessage <= 0)
                {
                    // Console.WriteLine("BlendSender: maxFloatsPerMessage must be positive.");
                    return;
                }

                var chunkIndex = 0;
                for (int i = 0; i < data.Count; i += maxFloatsPerMessage)
                {
                    int count = Math.Min(maxFloatsPerMessage, data.Count - i);
                    List<float> chunk = data.GetRange(i, count);

                    string address = messageTitle + "/" + chunkIndex;

                    object[] oscArgs = chunk.Cast<object>().ToArray(); // Simpler conversion

                    var oscMessage = new OscMessage(address, oscArgs);
                    try
                    {
                        _oscSender.Send(oscMessage);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"BlendSender: Error sending OSC message to {address}: {ex.Message}");
                    }

                    chunkIndex++;
                }
            }
            else
            {
                throw new ObjectDisposedException(nameof(BlendSender));
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
                if (_oscSender != null)
                {
                    _oscSender.Dispose();
                    _oscSender = null;
                }
            }
            _isDisposed = true;
        }
    }
} 