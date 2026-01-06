using Gtec.Chain.Common.SignalProcessingPipelines;
using System.Collections.Generic;
using UnityEngine;

namespace Gtec.Bandpower
{
    public class BandpowerBars : MonoBehaviour
    {
        private Dictionary<string, double> _currentMeanBandpower = null;
        private Dictionary<string, GameObject> _bars = null;
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

            _bars = new Dictionary<string, GameObject>();
            Transform[] bars = GetComponentsInChildren<Transform>();
            foreach (Transform bar in bars)
            {
                if (bar.name.Equals("delta"))
                    _bars.Add(BandpowerConstants.Delta, bar.gameObject);
                if (bar.name.Equals("theta"))
                    _bars.Add(BandpowerConstants.Theta, bar.gameObject);
                if (bar.name.Equals("alpha"))
                    _bars.Add(BandpowerConstants.Alpha, bar.gameObject);
                if (bar.name.Equals("beta_low"))
                    _bars.Add(BandpowerConstants.BetaLow, bar.gameObject);
                if (bar.name.Equals("beta_mid"))
                    _bars.Add(BandpowerConstants.BetaMid, bar.gameObject);
                if (bar.name.Equals("beta_high"))
                    _bars.Add(BandpowerConstants.BetaHigh, bar.gameObject);
                if (bar.name.Equals("gamma"))
                    _bars.Add(BandpowerConstants.Gamma, bar.gameObject);
            }
            if (_bars.Count != 7)
            {
                _bars = null;
                throw new System.Exception("Could not get bars.");
            }

            foreach (KeyValuePair<string, GameObject> kvp in _bars)
            {
                Vector3 pos = kvp.Value.transform.localPosition;
                pos.x = 0;
                kvp.Value.transform.localPosition = pos;
                kvp.Value.transform.transform.localScale = new Vector3(0, 0.2f, 0.2f);
            }
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
                _bci.OnMeanBandpowerAvailable.AddListener(SetMeanBandpowerRatios);
            }
        }

        public void RemoveEvents()
        {
            if (_bci != null)
            {
                _bci.OnMeanBandpowerAvailable.RemoveListener(SetMeanBandpowerRatios);
            }
        }

        void Update()
        {
            if (_currentMeanBandpower != null && _bars != null)
            {
                UpdateBar(BandpowerConstants.Delta);
                UpdateBar(BandpowerConstants.Theta);
                UpdateBar(BandpowerConstants.Alpha);
                UpdateBar(BandpowerConstants.BetaLow);
                UpdateBar(BandpowerConstants.BetaMid);
                UpdateBar(BandpowerConstants.BetaHigh);
                UpdateBar(BandpowerConstants.Gamma);
            }
        }

        public void UpdateBar(string frequencyband)
        {
            float val = (float)_currentMeanBandpower[frequencyband];
            if (val <= 0)
                val = 0;
            Vector3 pos = _bars[frequencyband].transform.localPosition;
            pos.x = val / 2;
            _bars[frequencyband].transform.localPosition = pos;
            _bars[frequencyband].transform.localScale = new Vector3(val, 0.2f, 0.2f);
        }

        public void SetMeanBandpowerRatios(Dictionary<string, double> bandpowerMean)
        {
            _currentMeanBandpower = bandpowerMean;
        }
    }

}