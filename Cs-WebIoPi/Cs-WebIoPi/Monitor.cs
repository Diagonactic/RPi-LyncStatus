using System;
using System.Threading;
using Microsoft.Lync.Model;
using Microsoft.Win32;

namespace CsWebIopi
{
    public class Monitor : ThreadSafeDisposableBase
    {
        private static readonly TimeSpan s_ErrorDelay = TimeSpan.FromMilliseconds(750), s_NormalDelay = TimeSpan.FromSeconds(3);

        private readonly LyncClient m_client;
        private readonly GpioController m_gpioController;
        private bool m_hasJustWokenUp, m_isGoingToSleep;
        private bool m_isErrorState;

        /// <summary>Begins monitoring the Skype for Business client for changes to the logged in user's presence to control the Raspberry Pi's LEDs</summary>
        /// <param name="ip">The IP Address of the WebIOOi interface</param>
        /// <param name="port">The Port that the REST service for WebIOPi is listening on</param>
        /// <param name="userId">The User ID to use when logging into the WebIOPi service</param>
        /// <param name="password">The Password to use when logging into the WebIOPi service</param>
        /// <remarks>See remarks on <see cref="GpioController" />.  Class assumes Available LED is on PIN 18, Away is on 17 and Busy is on 28. Change accordingly.</remarks>
        /// <param name="runTestSequence"></param>
        /// <exception cref="InvalidOperationException">Client must be logged in before starting the monitor</exception>
        public Monitor(string ip, ushort port, string userId, string password, bool runTestSequence = false)
        {
            m_gpioController = new GpioController(ip, port, userId, password);
            m_gpioController.AllOff();

            if (runTestSequence)
            {
                SimpleLogger.Log("To assist in checking your wiring, we'll run through a few tests of your rig . . .");

                m_gpioController.BlinkAll(s_ErrorDelay);
                Thread.Sleep(1000);
                SimpleLogger.Log("All LEDs should be blinking - Press any key to continue . . .");
                Console.ReadKey();
                m_gpioController.StopBlinking();
                
                SimpleLogger.Log("Setting Available ", m_gpioController.SetAvailable());
                SimpleLogger.Log("Available LED should be lit, all others should be off - Press any key to continue . . .");
                Console.ReadKey();

                SimpleLogger.Log("Setting Busy ", m_gpioController.SetBusy());
                SimpleLogger.Log("Busy LED should be lit, all others should be off - Press any key to continue . . .");
                Console.ReadKey();

                SimpleLogger.Log("Setting Away ", m_gpioController.SetAway());
                SimpleLogger.Log("Away LED should be lit, all others should be off - Press any key to continue . . .");
                Console.ReadKey();

                SimpleLogger.Log("Attempting to attach to Lync Client - any error received after this point is Lync related");
            }

            // Connect to the current Lync Client
            m_client = LyncClient.GetClient();

            var contact = m_client.Self.Contact;
            if (contact == null) // There's better ways to do this, but this works in a dirty implementation
            {
                SimpleLogger.Log("Client is not logged in - Setting Offline", m_gpioController.AllOff());
                throw new InvalidOperationException("Client must be logged in before starting the monitor");
            }

            // Create a subscription and subscribe to our own contact object
            contact.ContactInformationChanged += ContactOnContactInformationChanged;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            var contactSubscription = m_client.ContactManager.CreateSubscription();
            contactSubscription.Contacts.Add(contact);

            // Set the initial state of the LED (ContactInformationChanged does not fire on subscription)
            SetLedState();
        }

        

        /// <summary>
        ///     Called when <see cref="ThreadSafeDisposableBase.Dispose" /> is invoked. Thread safe and provides guarantees that the method will never be called more than once or throw
        ///     <see cref="ObjectDisposedException" /> if already disposed. This method will only execute if Dispose is called.
        /// </summary>
        /// <remarks>Implementers need not call <see langword="base" />.<see cref="ThreadSafeDisposableBase.DisposeManagedResources" />.</remarks>
        protected override void DisposeManagedResources()
        {
            m_client.Self.Contact.ContactInformationChanged -= ContactOnContactInformationChanged;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            m_gpioController.AllOff();
            m_gpioController.Dispose();
            Console.WriteLine("Exiting . . .");
        }

        private void SetLedState()
        {
            var contact = m_client.Self.Contact;
            if (contact == null)
            {
                // This happens when the client is signed out, so we'll set it offline
                SimpleLogger.Log("Setting Offline", m_gpioController.AllOff());
                return;
            }
            try
            {
                // Availability ID is encoded as an integer, not the actual ContactAvailability, so we'll get the int value
                // and cast it to ContactAvailability which contains all of the built in availability states.
                object availabilityId = contact.GetContactInformation(ContactInformationType.Availability);
                var availability = (ContactAvailability) availabilityId;
                SimpleLogger.Log($"Availability changed to {availability} ({availabilityId})");

                // This method only gets called from the constructor and the event firing method.  In either case, that
                // indicates that whatever error occurred has cleared, so stop the blinking and remove the error
                if (m_isErrorState)
                {
                    m_gpioController.StopBlinking();
                    m_isErrorState = false;
                }

                // Set the LEDs based on the availability options in the enum, or blink if an availability can't
                // be figured out
                switch (availability)
                {
                    case ContactAvailability.Busy:
                    case ContactAvailability.BusyIdle:
                    case ContactAvailability.DoNotDisturb:
                        if (!m_gpioController.IsBusyOn())
                            SimpleLogger.Log($"Setting Busy", m_gpioController.SetBusy());
                        break;
                    case ContactAvailability.FreeIdle:
                    case ContactAvailability.Away:
                    case ContactAvailability.TemporarilyAway:
                        if (!m_gpioController.IsAwayOn())
                            SimpleLogger.Log($"Setting Away", m_gpioController.SetAway());
                        break;
                    case ContactAvailability.Free:
                        if (!m_gpioController.IsAvailableOn())
                            SimpleLogger.Log($"Setting Available", m_gpioController.SetAvailable());
                        break;
                    case ContactAvailability.Offline:
                        SimpleLogger.Log($"Turning off all LEDs", m_gpioController.SetAvailable());
                        m_gpioController.AllOff();
                        break;
                    default:
                        m_gpioController.AllOff();
                        m_gpioController.BlinkAll(s_ErrorDelay);
                        m_isErrorState = true;
                        break;
                }
                m_hasJustWokenUp = false;
            }
            catch (NotSignedInException ex)
            {
                // Workaround for a strange Lync Client API behavior - The event for detecting a change in a contact's presence
                // fires indicating a change before we can actually read the presence.  When this happens, it throws the NotSignedInException
                // so if we've just woken up, we suppress it.  If something else has caused this to fire, we blink the lights
                if (m_hasJustWokenUp)
                    return;

                SimpleLogger.Log("Client is currently logged out so presence will not be processed");
                m_gpioController.BlinkAll(s_ErrorDelay);
                m_isErrorState = true;
            }
        }

        #region Event Handlers
        /// <summary>Turn off the network LEDs when this device sleeps and restart when it recovers</summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            switch (e.Mode)
            {
                case PowerModes.Suspend:
                    SimpleLogger.Log("Device is going to sleep - can't track state any longer. Setting all off ", m_gpioController.AllOff());
                    m_hasJustWokenUp = false;
                    m_isGoingToSleep = true;
                    break;
                case PowerModes.Resume:
                    SimpleLogger.Log("Device is waking up - presence will be set as soon as the client logs back in");
                    m_isGoingToSleep = false;
                    m_hasJustWokenUp = true;
                    break;
            }
        }

        private void ContactOnContactInformationChanged(object sender, ContactInformationChangedEventArgs e)
        {
            // Note that "sender" is the Contact object that the change occurred on.  We don't care about it for
            // this solution since we're only subscribing to the Self contact.

            // If we detect the suspend operation before the client does, this event will fire after we've turned the lights
            // off, so we prevent that here.
            if (m_isGoingToSleep)
                return;
            
            if (e.ChangedContactInformation.Contains(ContactInformationType.Availability))
                SetLedState();
        }
        #endregion
    }
}