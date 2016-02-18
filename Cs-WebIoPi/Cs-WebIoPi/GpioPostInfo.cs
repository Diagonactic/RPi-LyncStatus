namespace CsWebIopi
{
    /// <summary>Stores the Uri, Value and single flag that represents the light being toggled</summary>
    public class GpioPostInfo
    {
        /// <summary>
        /// Creates a new GpioPostInfo
        /// </summary>
        /// <param name="baseUri">The full base URI, including "/value" without the trailing "/" to the WebIOPi web service</param>
        /// <param name="gpioLight">The *single* flag representing the light that this GpioPostInfo is triggering</param>
        /// <param name="turnOn">When <see langword="true"/> the light will be turned on; otherwise it will be turned off</param>
        public GpioPostInfo(string baseUri, GpioLights gpioLight, bool turnOn)
        {
            TurnOn = turnOn;
            UriPostValue = turnOn ? 1 : 0;
            Uri = baseUri + "/" + UriPostValue;
            RepresentingLight = gpioLight;
        }

        /// <summary>
        /// The full URL that needs to be POSTed to
        /// </summary>
        public string Uri { get; }
        /// <summary>
        /// The value that will be POSTed (which is ignored, but required by WebClient - WebIOPi only cares about the URL that
        /// the POST operation occurs on).
        /// </summary>
        public int UriPostValue { get; }
        /// <summary>
        /// Are we turning the light on?
        /// </summary>
        public bool TurnOn { get; }
        /// <summary>
        /// The single light that this GpioPostInfo object is affecting
        /// </summary>
        public GpioLights RepresentingLight { get; }
    }
}