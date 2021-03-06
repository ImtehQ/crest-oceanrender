﻿// This file is subject to the MIT License as seen in the root of this folder structure (LICENSE)

// Shout out to @holdingjason who posted a first version of this script here: https://github.com/huwb/crest-oceanrender/pull/100

using Crest;
using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Boat physics by sampling at multiple probe points.
/// </summary>
public class BoatProbes : MonoBehaviour
{
    [Header("Forces")]
    [Tooltip("Override RB center of mass, in local space."), SerializeField]
    Vector3 _centerOfMass;
    [SerializeField] FloaterForcePoints[] ForcePoints;
    [SerializeField] float _forceHeightOffset = 0f;
    [SerializeField] float _forceMultiplier = 10f;
    [SerializeField] float _minSpatialLength = 12f;

    [Header("Drag")]
    [SerializeField] float _dragInWaterUp = 3f;
    [SerializeField] float _dragInWaterRight = 2f;
    [SerializeField] float _dragInWaterForward = 1f;

    [Header("Control")]
    [SerializeField] bool _playerControlled = true;
    [SerializeField] float EnginePower = 10;
    [SerializeField] float TurnPower = 0.5f;

    private const float WATER_DENSITY = 1000;

    Rigidbody _rb;

    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.centerOfMass = _centerOfMass;

        if (OceanRenderer.Instance == null)
        {
            enabled = false;
            return;
        }
    }

    private void FixedUpdate()
    {
        if (GPUReadbackDisps.Instance)
        {
            GPUReadbackDisps.Instance.ProcessRequests();
        }

        FixedUpdateEngine();
        FixedUpdateBuoyancy();
        FixedUpdateDrag();
    }

    void FixedUpdateEngine()
    {
        if (!_playerControlled)
            return;

        var forcePosition = _rb.position;

        var forward = Input.GetAxis("Vertical");
        _rb.AddForceAtPosition(transform.forward * EnginePower * forward, forcePosition, ForceMode.Acceleration);

        var sideways = (Input.GetKey(KeyCode.A) ? -1f : 0f) + (Input.GetKey(KeyCode.D) ? 1f : 0f);

        Vector3 heel = transform.forward;

        _rb.AddTorque((transform.up + heel) * TurnPower * sideways, ForceMode.Acceleration);
    }

    void FixedUpdateBuoyancy()
    {
        float archimedesForceMagnitude = WATER_DENSITY * Mathf.Abs(Physics.gravity.y);
        var collProvider = OceanRenderer.Instance.CollisionProvider;

        for (int i = 0; i < ForcePoints.Length; i++)
        {
            FloaterForcePoints point = ForcePoints[i];
            var transformedPoint = transform.TransformPoint(point._offsetPosition + new Vector3(0, _centerOfMass.y, 0));

            Vector3 undispPos;
            if (!collProvider.ComputeUndisplacedPosition(ref transformedPoint, out undispPos, _minSpatialLength))
            {
                // If we couldn't get wave shape, assume flat water at sea level
                undispPos = transformedPoint;
                undispPos.y = OceanRenderer.Instance.SeaLevel;
            }

            var waterSurfaceVel = Vector3.zero;

            bool dispValid, velValid;
            collProvider.SampleDisplacementVel(ref undispPos, out point._displaced, out dispValid, out waterSurfaceVel, out velValid, _minSpatialLength);

            var dispPos = undispPos + point._displaced;

            float height;
            collProvider.SampleHeight(ref transformedPoint, out height, _minSpatialLength);

            float distance = dispPos.y - transformedPoint.y;

            if (height - transformedPoint.y > 0)
            {
                _rb.AddForceAtPosition(archimedesForceMagnitude * distance * Vector3.up * point._factor * _forceMultiplier, transformedPoint);
            }
        }
    }
    
    void FixedUpdateDrag()
    {
        // Apply drag relative to water
        var collProvider = OceanRenderer.Instance.CollisionProvider;

        var pos = _rb.position;
        Vector3 undispPos;
        if (!collProvider.ComputeUndisplacedPosition(ref pos, out undispPos, _minSpatialLength))
        {
            // If we couldn't get wave shape, assume flat water at sea level
            undispPos = pos;
            undispPos.y = OceanRenderer.Instance.SeaLevel;
        }

        Vector3 displacement;
        var waterSurfaceVel = Vector3.zero;
        bool dispValid, velValid;
        collProvider.SampleDisplacementVel(ref undispPos, out displacement, out dispValid, out waterSurfaceVel, out velValid, _minSpatialLength);

        var _velocityRelativeToWater = _rb.velocity - waterSurfaceVel;

        var forcePosition = _rb.position + _forceHeightOffset * Vector3.up;
        _rb.AddForceAtPosition(Vector3.up * Vector3.Dot(Vector3.up, -_velocityRelativeToWater) * _dragInWaterUp, forcePosition, ForceMode.Acceleration);
        _rb.AddForceAtPosition(transform.right * Vector3.Dot(transform.right, -_velocityRelativeToWater) * _dragInWaterRight, forcePosition, ForceMode.Acceleration);
        _rb.AddForceAtPosition(transform.forward * Vector3.Dot(transform.forward, -_velocityRelativeToWater) * _dragInWaterForward, forcePosition, ForceMode.Acceleration);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawCube(transform.TransformPoint(_centerOfMass), Vector3.one * 0.25f);

        for (int i = 0; i < ForcePoints.Length; i++)
        {
            var point = ForcePoints[i];

            var transformedPoint = transform.TransformPoint(point._offsetPosition + new Vector3(0, _centerOfMass.y, 0));

            Gizmos.color = Color.red;
            Gizmos.DrawCube(transformedPoint, Vector3.one * 0.5f);
        }
    }
}

[Serializable]
public class FloaterForcePoints
{
    public float _factor = 1f;

    [FormerlySerializedAs("_offSetPosition")]
    public Vector3 _offsetPosition;

    [NonSerialized]
    public Vector3 _displaced;
}
