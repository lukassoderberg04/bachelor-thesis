using System.Net;

namespace pm1000_streamer_service.API
{
    /// <summary>
    /// The REST api is defined in this class. It will serve the REST functionality to the users.
    /// Port 5002 --> rest api:
    ///    GET:  /frequency  --> laser light frequency in Hz.
    ///    GET:  /samplerate --> current sampling rate in Hz.
    ///    POST: /samplerate --> set a new sampling rate in Hz.
    /// </summary>
    public static class RestServer
    {
        public static readonly int REST_PORT = 5002;

        /// <summary>
        /// Start the REST service.
        /// </summary>
        public static Task Start(CancellationToken token)
        {
            Logger.LogInfo("Starting REST server on a different thread...");

            return Task.Run(() => runRestServer(token));
        }

        /// <summary>
        /// Configures the REST server and starts handling new requests.
        /// </summary>
        private async static Task runRestServer(CancellationToken token)
        {
            using var listener = new HttpListener();
            listener.Prefixes.Add($"http://{IPAddress.Loopback}:{REST_PORT}/");

            Logger.LogInfo($"Configured REST server to listen on: {listener.Prefixes.FirstOrDefault("NOT CONFIGURED")}.");

            try
            {
                listener.Start();

                Logger.LogInfo("REST server is now listening for incoming requests!");

                await acceptAndHandleRequests(listener, token);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"REST server stopped with exception: {ex.Message}.");
            }
        }

        /// <summary>
        /// Handles the incoming requests and sends back a response.
        /// </summary>
        private async static Task acceptAndHandleRequests(HttpListener listener, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync();

                /*
                    * Check what type of request it was.
                    * Send bytes in correspondance to the request to the sender.
                    * Close connection.
                */

                /*
                    * [GET]  /frequency  => Laser light frequency in Hz.
                    * [GET]  /samplerate => Current sampling rate in Hz.
                    * [POST] /samplerate => Set a new sampling rate.
                */

                throw new NotImplementedException();
            }
        }
    }
}
