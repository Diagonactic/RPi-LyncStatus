using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using Diagonactic;

namespace CsWebIopi
{
    /// <summary>Class that sends commands to the WebIOPi interface</summary>
    public class GpioController : ThreadSafeDisposableBase
    {
        private readonly string m_availableUri;
        private readonly string m_userId;
        private readonly string m_password;
        private readonly string m_awayUri;
        private readonly string m_busyUri;
        private readonly Timer m_timer;
        private int m_isBlinking = 0;

        /// <summary>The last state that the lights were set to (this is not a detected state, but rather the state that the application last set or has blinking)</summary>
        private InterlockedEnum<GpioLights> m_lightsStateOn = new InterlockedEnum<GpioLights>(GpioLights.None);

        /// <summary>Initializes a new instance of <see cref="GpioController" /> for interacting with WebIOPi on a Raspberry Pi</summary>
        /// <param name="ipAddress">The IP Address of the WebIOOi interface</param>
        /// <param name="port">The Port that the REST service for WebIOPi is listening on</param>
        /// <param name="userId">The User ID to use when logging into the WebIOPi service</param>
        /// <param name="password">The Password to use when logging into the WebIOPi service</param>
        /// <param name="availablePin">The GPIO pin that the Availability LED is plugged into</param>
        /// <param name="awayPin">The GPIO pin that the Away LED is plugged into</param>
        /// <param name="busyPin">The GPIO pin that the Busy LED is plugged into</param>
        /// <remarks>
        ///     <para>Take care when wiring the LEDs for GPIO usage. Make sure to install appropriate resitors!</para>
        ///     <para>
        ///         WebIOPi must be configured on the device before this can be used.  The ports with the LEDs attached should be set to OUT.  A user ID and password should be configured
        ///         for the service (sudo webiopi-passwd)
        ///     </para>
        ///     <para>
        ///         For assistance wiring, I used this link <see href="https://projects.drogon.net/raspberry-pi/gpio-examples/tux-crossing/2-two-more-leds/">Wiring two or more LEDs</see>
        ///     </para>
        /// </remarks>
        public GpioController(string ipAddress, ushort port, string userId, string password, int availablePin = 18, int awayPin = 17, int busyPin = 27)
        {
            var raspiIp = IPAddress.Parse(ipAddress);

            m_availableUri = $"http://{raspiIp}:{port}/GPIO/{availablePin}/value";
            m_awayUri = $"http://{raspiIp}:{port}/GPIO/{awayPin}/value";
            m_busyUri = $"http://{raspiIp}:{port}/GPIO/{busyPin}/value";

            m_userId = userId;
            m_password = password;

            // Create timer that doesn't start and doesn't signal
            m_timer = new Timer(PerformBlinkingAction, null, Timeout.Infinite, -1);
        }

        /// <summary>
        ///     Called when <see cref="ThreadSafeDisposableBase.Dispose" /> is invoked. Thread safe and provides guarantees that the method will never be called more than once or throw
        ///     <see cref="ObjectDisposedException" /> if already disposed. This method will only execute if Dispose is called.
        /// </summary>
        /// <remarks>Implementers need not call <see langword="base" />.<see cref="ThreadSafeDisposableBase.DisposeManagedResources" />.</remarks>
        protected override void DisposeManagedResources()
        {
            m_timer.Dispose();
        }

        /// <summary>
        /// Is the light blinking?
        /// </summary>
        public bool IsBlinking => Interlocked.CompareExchange(ref m_isBlinking, 1, 1) == 1;
        
        private TimeSpan m_currentBlinkInterval = TimeSpan.Zero;

        /// <summary>
        /// Blinks the <paramref name="lightsToBlink"/> at the <paramref name="blinkInterval"/> interval
        /// </summary>
        /// <param name="lightsToBlink">Which lights to blink</param>
        /// <param name="blinkInterval">How long to leave them on and off</param>
        private void EnableBlinking(GpioLights lightsToBlink, TimeSpan blinkInterval)
        {
            // Check that the light is actually NOT blinking, and set it to blinking in a
            // thread safe manner
            if (Interlocked.CompareExchange(ref m_isBlinking, 1, 0) == 0)
            {
                m_lightsStateOn.Value = lightsToBlink;
                m_currentBlinkInterval = blinkInterval;
                m_timer.Change(blinkInterval.Milliseconds, blinkInterval.Milliseconds * 2);
            }
                
        }

        /// <summary>
        /// Stops the lights from blinking.
        /// </summary>
        /// <remarks>For safety, it sleeps the calling thread by the twice length of time originally specified to blink to ensure that
        /// turning all of the LEDs off completes.  This, obviously doesn't help much if the thread calling this is different than a 
        /// future thread that sets the LED state</remarks>
        public void StopBlinking()
        {
            // Check that the light is actually blinking and set it to NOT blinking in a threadsafe manner
            if (Interlocked.CompareExchange(ref m_isBlinking, 0, 1) == 1)
            {
                m_timer.Change(Timeout.Infinite, -1);
                Thread.Sleep(m_currentBlinkInterval.Milliseconds * 2);
            }
        }
        /// <summary>
        ///     Given a set of <paramref name="onLights" /> flags, returns a dictionary containing the URL and the value to set for each light to cause that state to be displayed from
        ///     the LEDs
        /// </summary>
        /// <param name="onLights"></param>
        /// <returns></returns>
        private IEnumerable<GpioPostInfo> GetLightUrlValues(GpioLights onLights)
            => new[]
               {
                   new GpioPostInfo(m_availableUri, GpioLights.Available, onLights.IsFlagSet(GpioLights.Available)),
                   new GpioPostInfo(m_awayUri, GpioLights.Away, onLights.IsFlagSet(GpioLights.Away)),
                   new GpioPostInfo(m_busyUri, GpioLights.Busy, onLights.IsFlagSet(GpioLights.Busy))
               };

        private void PerformBlinkingAction(object state)
        {
            SetLights(m_lightsStateOn.Value);
            Thread.Sleep(m_currentBlinkInterval);
            AllOff();
        }

        /// <summary>
        /// Queries the web service to see if the available LED is turned on
        /// </summary>
        /// <returns>When successful, returns <see langword="true"/>; otherwise <see langword="false"/></returns>
        public bool IsAvailableOn() => IsOn(m_availableUri);

        /// <summary>
        /// Queries the web service to see if the away LED is on
        /// </summary>
        /// <returns>When successful, returns <see langword="true"/>; otherwise <see langword="false"/></returns>
        public bool IsAwayOn() => IsOn(m_awayUri);

        /// <summary>
        /// Queries the web service to see if the Busy LED is on
        /// </summary>
        /// <returns>When successful, returns <see langword="true"/>; otherwise <see langword="false"/></returns>
        public bool IsBusyOn() => IsOn(m_busyUri);

        private bool IsOn(string uri)
        {
            using (var wc = new CredentialedWebClient(m_userId, m_password))
                return wc.DownloadString(uri) == "1";
        }

        private static bool PostToWebIoPi(WebClient wc, GpioPostInfo gpioPostInfo)
        {
            try
            {
                wc.UploadString(gpioPostInfo.Uri, gpioPostInfo.UriPostValue.ToString());
                Debug.WriteLine($"Posting {gpioPostInfo.UriPostValue} to {gpioPostInfo.Uri}");
                return true;
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"Attempted to {gpioPostInfo.RepresentingLight.AsString()} to {(gpioPostInfo.TurnOn ? "ON" : "OFF")} using {gpioPostInfo.Uri} failed\r\n{ex}");
                return false;
            }
        }

        private bool SetAllLightsTo(bool turnOn)
        {
            return SetLights(turnOn ? GpioLights.None.AddFlags(GpioLights.Available, GpioLights.Away, GpioLights.Busy) : GpioLights.None);
        }

        private bool SetLights(GpioLights valueToSet)
        {
            var postTo = GetLightUrlValues(valueToSet);

            using (var wc = new CredentialedWebClient(m_userId, m_password))
                foreach (var postInfo in postTo)
                    if (PostToWebIoPi(wc, postInfo))
                        m_lightsStateOn.Value.ModifyFlag(postInfo.RepresentingLight, postInfo.TurnOn);
                    else // If we fail to set any light, stop trying to set lights and just return false
                        return false;

            return true;
        }

        /// <summary>
        /// Turns all of the lights off
        /// </summary>
        /// <remarks>Do not call when lights are blinking - this will stop the blinking but the background operation that performs
        /// the blink will contine to run, resulting in any newly turned on lights going into a blink state</remarks>
        /// <returns>When successful, returns <see langword="true"/>; otherwise <see langword="false"/></returns>
        public bool AllOff() => SetAllLightsTo(false);

        /// <summary>
        /// Turns all of the lights on
        /// </summary>
        /// <remarks>If called during blinking, the lights will all blink on</remarks>
        /// <returns>When successful, returns <see langword="true"/>; otherwise <see langword="false"/></returns>
        public bool AllOn() => SetAllLightsTo(true);

        /// <summary>
        /// Turns on the Available LED and turns off all other LEDs
        /// </summary>
        /// <returns>When successful, returns <see langword="true"/>; otherwise <see langword="false"/></returns>
        public bool SetAvailable() => SetLights(GpioLights.Available);

        /// <summary>
        /// Turns on the Away LED and turns off all other LEDs
        /// </summary>
        /// <returns>When successful, returns <see langword="true"/>; otherwise <see langword="false"/></returns>
        public bool SetAway() => SetLights(GpioLights.Away);

        /// <summary>
        /// Turns on the Busy LED and turns off all other LEDs
        /// </summary>
        /// <returns>When successful, returns <see langword="true"/>; otherwise <see langword="false"/></returns>
        public bool SetBusy() => SetLights(GpioLights.Busy);

        /// <summary>
        /// Blinks all LEDs
        /// </summary>
        /// <param name="blinkingInterval">Length of time to leave the lights on and off (timer is called at twice this interval)</param>
        public void BlinkAll(TimeSpan blinkingInterval) => EnableBlinking(GpioLights.None.AddFlags(GpioLights.Available, GpioLights.Away, GpioLights.Busy), blinkingInterval);
    }
}