using UnityEngine;
using System.Collections;
using UnityEngine.UI;

#if WINDOWS_UWP
using System.Threading.Tasks;
using Microsoft.Tools.WindowsDevicePortal;
#endif

public class FBController : MonoBehaviour
{
    #region Constants
    // How often the update loop polls the portal for IPD updates
    private const float UPDATE_PERIOD = 5.0f;
    #endregion // Constants

    #region Inspector Fields
    [Tooltip("GameObject that contains all elements for the Error state.")]
    public GameObject errorState;

    [Tooltip("Text control used to display the current IPD.")]
    public Text ipdText;

    [Tooltip("GameObject that contains all elements for the Loaded state.")]
    public GameObject loadedState;

    [Tooltip("Text control used to display status messages.")]
    public Text statusText;

    [Tooltip("GameObject that contains all elements for the Updating state.")]
    public GameObject updatingState;
    #endregion // Inspector Fields

    #region Member Variables
    #if WINDOWS_UWP
    private DevicePortal portal;
    #endif
    #endregion // Member Variables

    #region Internal Methods
    #if WINDOWS_UWP
    private async Task EnsurePortalAsync()
    {
        if (portal == null)
        {
            portal = new DevicePortal(
                new DefaultDevicePortalConnection(
                    "https://127.0.0.1",
                    "???",
                    "???"));

            await portal.Connect();
        }
    }

    private async void ReadValues()
    {
        try
        {
            // Wait for connection
            await EnsurePortalAsync();

            // Get IPD
            float ipd = await portal.GetInterPupilaryDistance();

            // Show IPD on Unity thread
            UnityEngine.WSA.Application.InvokeOnAppThread(() => { ShowIpd(ipd); }, false);
        }
        catch
        {
            // Show error on Unity thread
            UnityEngine.WSA.Application.InvokeOnAppThread(ShowError, false);
        }
    }
    #else
    private void ReadValues() { }
    #endif

    /// <summary>
    /// Shows the error state.
    /// </summary>
    private void ShowError()
    {
        // Set proper state
        errorState.SetActive(true);
        loadedState.SetActive(false);
        updatingState.SetActive(false);
    }

    /// <summary>
    /// Shows the specified IPD.
    /// </summary>
    /// <param name="ipd">
    /// The IPD to show.
    /// </param>
    private void ShowIpd(float ipd)
    {
        // Update text
        ipdText.text = ipd.ToString();

        // Set proper state
        errorState.SetActive(false);
        loadedState.SetActive(true);
        updatingState.SetActive(false);
    }

    /// <summary>
    /// Shows the update state.
    /// </summary>
    /// <param name="status">
    /// The status message text.
    /// </param>
    private void ShowUpdate(string status)
    {
        // Update text field
        statusText.text = status;

        // Set proper state
        errorState.SetActive(false);
        loadedState.SetActive(false);
        updatingState.SetActive(true);
    }

    /// <summary>
    /// Loop that polls device portal and updates the UI.
    /// </summary>
    private IEnumerator UpdateLoop()
    {
        while (true)
        {
            // Set update state
            ShowUpdate("Reading IPD...");

            // Actually read IPD
            ReadValues();

            // Wait for next poll
            yield return new WaitForSeconds(UPDATE_PERIOD);
        }
    }
    #endregion // Internal Methods

    #region Behaviour Overrides
    // Use this for initialization
    void Start()
    {
        StartCoroutine(UpdateLoop());
    }
#endregion // Behaviour Overrides
}
