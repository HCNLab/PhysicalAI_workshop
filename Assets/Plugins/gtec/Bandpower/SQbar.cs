using Gtec.Chain.Common.Nodes.InputNodes;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gtec.Bandpower
{
    public class SQbar : MonoBehaviour
    {
        private Device _bci;
        private GameObject[] _gameObjects;
        private Renderer[] _renderers;

        // Start is called before the first frame update
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

            Transform[] objects = GetComponentsInChildren<Transform>();
            for(int i = objects.Length -1; i >= 0; i--)
                if(objects[i].name.Contains("ch"))
                    GameObject.Destroy(objects[i].gameObject);
            _gameObjects = null;
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
                _bci.OnSignalQualityAvailable.AddListener(SetSQ);
            }
        }

        private void SetSQ(List<ChannelQuality.ChannelStates> channelStates)
        {
            if(_gameObjects != null && _gameObjects.Count() != channelStates.Count())
            {
                foreach (GameObject go in _gameObjects)
                    Destroy(go);
                _gameObjects = null;
            }

            if (_gameObjects == null)
            {
                _gameObjects = new GameObject[channelStates.Count];
                _renderers = new Renderer[channelStates.Count];
                for (int i = 0; i < _gameObjects.Length; i++)
                {
                    _gameObjects[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    _gameObjects[i].transform.SetParent(this.gameObject.transform);
                    _gameObjects[i].transform.localScale = Vector3.one;
                    float offset = (_gameObjects.Length * 1.0f + (_gameObjects.Length - 1) * 0.2f) * 0.5f - 0.5f;
                    float xpos = -offset + i * 1.2f;
                    _gameObjects[i].transform.transform.localPosition = new Vector3(xpos, 0, 0);
                    _gameObjects[i].name = "ch" + i;
                    _renderers[i] = _gameObjects[i].GetComponent<Renderer>();
                }
            }

            for (int i = 0; i < _gameObjects.Length; i++)
            {
                Renderer renderer = _gameObjects[i].GetComponent<Renderer>();
                if (channelStates[i] == ChannelQuality.ChannelStates.Good)
                    renderer.material.color = Color.green;
                else if(channelStates[i] == ChannelQuality.ChannelStates.BadFloating)
                    renderer.material.color = Color.red;
                else
                    renderer.material.color = Color.yellow;
            }
        }

        public void RemoveEvents()
        {
            if (_bci != null)
            {
                _bci.OnSignalQualityAvailable.RemoveListener(SetSQ);
            }
        }
    }
}