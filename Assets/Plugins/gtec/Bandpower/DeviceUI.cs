using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static Gtec.Chain.Common.Templates.DataAcquisitionUnit.DataAcquisitionUnit;

namespace Gtec.Bandpower
{
    public class DeviceUI : MonoBehaviour
    {
        public UnityEvent<string> OnConnect;
        public UnityEvent OnDisconnect;

        private TMP_Dropdown _ddDevices;
        private Button _btnConnect;
        private TextMeshProUGUI _btnText;
        private bool _connected;
        private Device _bci;
        void Start()
        {
            try
            {
                _bci = GetComponentInParent<Device>();
            }
            catch
            {
                _bci = null;
            }
            AttachEvents();

            _ddDevices = this.GetComponentInChildren<TMP_Dropdown>();

            if (_ddDevices == null)
                throw new System.Exception("Could not get dropdown UI element");
            _ddDevices.ClearOptions();

            Button[] buttons = this.GetComponentsInChildren<Button>();
            foreach (Button button in buttons)
            {
                if (button.name.Equals("btnConnect"))
                    _btnConnect = button;
            }

            if (_btnConnect == null)
                throw new System.Exception("Could not get button UI element");

            _btnText = _btnConnect.GetComponentInChildren<TextMeshProUGUI>();

            if (_btnText == null)
                throw new System.Exception("Could not get button text UI element");

            _connected = false;

            _btnConnect.onClick.AddListener(btnConnect_OnClick);
        }

        private void OnDestroy()
        {
            RemoveEvents();
            _bci = null;
        }

        private void OnApplicationQuit()
        {
            RemoveEvents();
            _bci = null;
        }

        public void AttachEvents()
        {
            if (_bci != null)
            {
                _bci.OnDevicesAvailable.AddListener(UpdateAvailableDevices);
                _bci.OnDeviceStateChanged.AddListener(OnDeviceStateChanged);
            }
        }

        public void RemoveEvents()
        {
            if (_bci != null)
            {
                _bci.OnDevicesAvailable.RemoveListener(UpdateAvailableDevices);
                _bci.OnDeviceStateChanged.RemoveListener(OnDeviceStateChanged);
            }
        }

        public void UpdateAvailableDevices(List<string> devices)
        {
            _ddDevices.ClearOptions();
            if (devices != null && devices.Count > 0)
                _ddDevices.AddOptions(devices);
        }

        public void OnDeviceStateChanged(States state)
        {
            if (state == States.Connected)
            {
                _btnConnect.enabled = true;
                _ddDevices.enabled = false;
                _btnText.text = "Disconnect";
                _connected = true;
            }

            if(state == States.Connecting)
            {
                _btnConnect.enabled = false;
                _ddDevices.enabled = false;
                _btnText.text = "Connecting...";
                _connected = false;
            }

            if (state == States.Disconnected)
            {
                _btnConnect.enabled = true;
                _ddDevices.enabled = true;
                _btnText.text = "Connect";
                _connected = false;
            }
        }

        private void btnConnect_OnClick()
        {
            if (!_connected)
            {
                string serial = _ddDevices.options[_ddDevices.value].text;

                if (_bci != null)
                    _bci.Connect(serial);

                OnConnect.Invoke(serial);
            }
            else
            {
                if (_bci != null)
                    _bci.Disconnect();

                OnDisconnect.Invoke();
            }
        }
    }
}