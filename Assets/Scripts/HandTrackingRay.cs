using System.Collections.Generic;
using System;
using System.Collections;
using UnityEngine;
using UnityEditor;
using Oculus.Interaction.Input;

using TMPro;

namespace Oculus.Interaction.Input
{
    public class HandTrackingRay : MonoBehaviour
    {
        [SerializeField]
        public IHand Hand { get; private set; }
        public GameObject controllerUsedForPinch;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            
        }

        // Update is called once per frame
        void Update()
        {
            
        }
    }
    }