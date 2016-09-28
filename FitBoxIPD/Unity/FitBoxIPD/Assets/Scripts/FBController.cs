using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.Windows.Speech;
using System;
using HoloToolkit.Unity;
using Adept;

#if WINDOWS_UWP
using Microsoft.Tools.WindowsDevicePortal;
using System.Linq;
using System.Threading.Tasks;
using Windows.Security.Credentials;
#endif

public enum FBControllerState
{
    /// <summary>
    /// The controller is still initializing.
    /// </summary>
    Initializing,

    /// <summary>
    /// Attempting silent authentication
    /// </summary>
    SilentAuth,

    /// <summary>
    /// Attempting interactive login.
    /// </summary>
    LoggingIn,
    
    /// <summary>
    /// The user is logged out.
    /// </summary>
    LoggedOut,

    /// <summary>
    /// The user is logged in.
    /// </summary>
    LoggedIn
};

public class FBController : MonoBehaviour
{
    #region Constants
    private const bool IS_SIDELOADED = true;                // Whether this is a side loaded or store build
    static public readonly string PortalResourceName = "Device Portal";   // Resource name used to store the password in password vault
    private const float UPDATE_PERIOD = 5.0f;               // How often the update loop polls the portal for IPD updates
    #endregion // Constants

    #region Inspector Fields
    [Tooltip("Text control used to display error messages.")]
    public Text errorText;

    [Tooltip("Text control used to display the current IPD.")]
    public Text ipdText;

    [Tooltip("GameObject that contains all elements for the Logged In state.")]
    public GameObject loggedInState;

    [Tooltip("Manager used for speaking text.")]
    public TextToSpeechManager textToSpeech;
    #endregion // Inspector Fields

    #region Member Variables
    private AppViewInfo authView;
    private DictationRecognizer dictationRecognizer;
    private FBControllerState state;

    #if WINDOWS_UWP
    private DevicePortal portal;
    private PasswordVault vault;
#endif
    #endregion // Member Variables

    #region Internal Methods
    #if WINDOWS_UWP
    /// <summary>
    /// The Authentication State Machine
    /// </summary>
    private async void AuthStateMachine()
    {
        switch (state)
        {
            case FBControllerState.Initializing:
                // Now doing silent auth
                State = FBControllerState.SilentAuth;

                // Attempt to authenticate in case already cached credentials
                if (await AuthSilentAsync())
                {
                    State = FBControllerState.LoggedIn;
                }
                else
                {
                    // Not authenticated. Start login flow.
                    State = FBControllerState.LoggingIn;
                    await StartLoginAsync();
                }
                break;

            case FBControllerState.LoggingIn:
                // Just returned from login dialog. Attempt silent auth again.
                if (await AuthSilentAsync())
                {
                    State = FBControllerState.LoggedIn;
                }
                else
                {
                    State = FBControllerState.LoggedOut;
                }
                break;
        }

        // If we get here and we're logged in, start the update loop
        if (state == FBControllerState.LoggedIn)
        {
            StartCoroutine(UpdateLoop());
        }
    }

    private async Task<bool> AuthSilentAsync()
    {
        // Placeholder
        PasswordCredential cred = null;

        // Try to get user name and password
        try
        {
            cred = vault.FindAllByResource(PortalResourceName).FirstOrDefault();

            // Password does not come across with the method above. We must call another method.
            if (cred != null)
            {
                cred = vault.Retrieve(PortalResourceName, cred.UserName);
            }
        }
        catch { }

        // If no credentials were found, fail
        if (cred == null) { return false; }

        // Credentials found. Try and log into portal
        try
        {
            // Create portal object
            portal = new DevicePortal(
                new DefaultDevicePortalConnection(
                    "https://127.0.0.1",
                    cred.UserName,
                    cred.Password));

            // Attempt to connect
            await portal.Connect();

            // Get IPD
            var ipd = await portal.GetInterPupilaryDistance();

            // Success!
            return true;
        }
        catch (Exception ex)
        {
            // Problem
            ShowError(ex.Message);
            return false;
        }
    }

    private async void ReadValues()
    {
        try
        {
            // Get IPD
            float ipd = await portal.GetInterPupilaryDistance();

            // Show IPD
            ShowIpd(ipd);
        }
        catch (Exception ex)
        {
            // Show error on Unity thread
            ShowError(ex.Message);
        }
    }

    /// <summary>
    /// Starts the login (authentication) process by switching to XAML.
    /// </summary>
    private async Task StartLoginAsync()
    {
        // Get auth view if not already obtained
        if (authView == null)
        {
            authView = AppViewManager.Views["Auth"];
            authView.Consolidated += AuthView_Consolidated;
        }

        // Switch to auth view
        await authView.SwitchAsync();
    }

    private async void UpdateIpd(float ipd)
    {
        try
        {
             // Set IPD
            await portal.SetInterPupilaryDistance(ipd);

            // Reread values
            ReadValues();

            // Define message to speak
            var speakText = string.Format("IPD set to {0}", ipd);

            // Speak the message
            textToSpeech.SpeakText(speakText);
        }
        catch (Exception ex)
        {
            // Show error on Unity thread
            ShowError(ex.Message);
        }
    }

    #else
    private void AuthenticateAsync() { }
    private void ReadValues() { }
    private void UpdateIpd(float ipd)   {  }
    #endif

    /// <summary>
    /// Shows the error state.
    /// </summary>
    /// <param name="error">
    /// The error message to display.
    /// </param>
    private void ShowError(string error)
    {
        ThreadExtensions.RunOnAppThread(() =>
        {
            errorText.text = error;
        });
    }

    /// <summary>
    /// Shows the specified IPD.
    /// </summary>
    /// <param name="ipd">
    /// The IPD to show.
    /// </param>
    private void ShowIpd(float ipd)
    {
        ThreadExtensions.RunOnAppThread(() =>
        {
            ipdText.text = ipd.ToString();
        });
    }

    /// <summary>
    /// Loop that polls device portal and updates the UI.
    /// </summary>
    private IEnumerator UpdateLoop()
    {
        while (true)
        {
            // Actually read IPD
            ReadValues();

            // Start or restart the Dictation Recognizer in case it has paused
            if (dictationRecognizer.Status != SpeechSystemStatus.Running)
            {
                dictationRecognizer.Start();
            }

            // Wait for next poll
            yield return new WaitForSeconds(UPDATE_PERIOD);
        }
    }

    private void UpdateVisualState()
    {
        ThreadExtensions.RunOnAppThread(() =>
        {
            switch (state)
            {
                case FBControllerState.LoggedIn:
                    loggedInState.SetActive(true);
                    break;
                default:
                    loggedInState.SetActive(false);
                    break;
            }
        });
    }
    #endregion // Internal Methods

    #region Behaviour Overrides
    void Start()
    {
        if (IS_SIDELOADED)
        {
            // Create recognizer and subscribe
            dictationRecognizer = new DictationRecognizer();
            dictationRecognizer.DictationResult += DictationRecognizer_DictationResult;

            // Create the valut
            vault = new PasswordVault();

            // Start authentication state machine
            AuthStateMachine();
        }
        else
        {
            // Store builds cannot log in
            State = FBControllerState.LoggedOut;
        }
    }

    void OnDestroy()
    {
        dictationRecognizer.DictationResult -= DictationRecognizer_DictationResult;
        dictationRecognizer.Dispose();
    }
    #endregion // Behaviour Overrides

    #region Overrides / Event Handlers
    private void AuthView_Consolidated(object sender, EventArgs e)
    {
        // Auth view has been closed. Continue auth state machine.
        AuthStateMachine();
    }

    private void DictationRecognizer_DictationResult(string text, ConfidenceLevel confidence)
    {
        // To upper for checking
        var utext = text.ToUpper();

        // Look for magic words
        if (utext.Contains("SET") || utext.Contains("IPD"))
        {
            // Placeholders
            float mValue = 0;

            // Split
            string[] mWords = text.Split(' ');

            // Look for a float
            for (int i = 0; i< mWords.Length; i++)
            {
                // Try to parse. If successful, stop looking.
                if (float.TryParse(mWords[i], out mValue))
                {
                    break;
                }
            }

            if (mValue >= 55 && mValue <= 75)
            {
                ShowError("");
                UpdateIpd(mValue);
            }
            else
            {
                ShowError("IPD must be between 55 - 75");
            }
        }
    }
    #endregion // Overrides / Event Handlers

    #region Public Properties
    /// <summary>
    /// Gets the current state of the controller.
    /// </summary>
    public FBControllerState State
    {
        get
        {
            return state;
        }
        set
        {
            if (state != value)
            {
                state = value;
                UpdateVisualState();
            }
        }
    }
    #endregion // Public Properties
}
