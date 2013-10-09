using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SolarSail
{
	public class ModuleSolarSail : PartModule
	{
		// How much thrust will be gain when certain amount of light is projected onto the panel.
		[KSPField]
		public float thrustCoefficient = 0.5f;

		// How much part of particles are reflected to transfer momentum to the panel.
		// 1.0 makes it a pure reflecting solar sail.
		// Otherwise you can use magnetic field to deflect the rest of the particles to gain extra momentum.
		[KSPField]
		public float reflectedPhotonRatio = 0.5f;

		// Surface area of the panel.
		[KSPField]
		public float surfaceArea;

		// Magnetic mode. Use electric to generate magnetic field to deflect particles inbound.
		[KSPField(isPersistant = true, guiActive = true, guiName = "Magnetic Mode")]
		private bool magneticModeActivated = false;

		[KSPField(guiActive = true, guiName = "Force", guiFormat = "F3")]
		private float forceAcquired = 0.0f;

		// How much electric charge is consumed for each unit of surface area.
		[KSPField]
		public float magneticModeElectricConsumptionRate;

		// How much thrust can we gain from each unit of electric charge we consumed.
		[KSPField]
		public float powerToThrustRatio = 1.0f;

		// Solar power curve (distance's function).
		[KSPField]
		public FloatCurve solarPowerCurve = new FloatCurve();

		[KSPField]
		public string surfaceTransformName;

		private List<bool> isSunLightReached = new List<bool>();
 
		private Transform surfaceTransform = null;
		private ModuleAnimateGeneric solarSailAnim = null;

		public override void OnStart(StartState state)
		{
			if (state != StartState.None && state != StartState.Editor)
			{
				surfaceTransform = part.FindModelTransform(surfaceTransformName);
				solarSailAnim = (ModuleAnimateGeneric)part.Modules["ModuleAnimateGeneric"];
			}
		}

		public override void OnUpdate()
		{
			if(FlightGlobals.fetch != null)
			{
				float sunlightFactor = Mathf.Clamp01(solarSailAnim.Progress);
				Vector3 sunVector = FlightGlobals.fetch.bodies[0].position - part.orgPos;
				//Debug.Log("Sun Vector: " + sunVector.ToString());
				//float sunDistance = sunVector.magnitude;
				//Debug.Log("Sun Distance: " + sunDistance.ToString());
				//sunVector.Normalize();
				//Vector3 ownOccludeAvoidanceVector = sunVector * 400.0f;
				//Debug.Log("ownOccludeAvoidanceVector: " + ownOccludeAvoidanceVector.ToString());
				//sunVector *= Convert.ToSingle(sunDistance - FlightGlobals.fetch.bodies[0].Radius * 1.5f - 400.0f);
				//Debug.Log("Sun Vector (Clipped): " + sunVector.ToString());
				//RaycastHit hitInfo;
				//bool sunlightReached = !Physics.Raycast(part.orgPos + ownOccludeAvoidanceVector, sunVector, out hitInfo, sunVector.magnitude, 1 << 15);

				bool sunlightReached = !Physics.Raycast(part.orgPos, sunVector.normalized, sunVector.magnitude);
				while (isSunLightReached.Count >= 30)
					isSunLightReached.RemoveAt(0);
				isSunLightReached.Add(sunlightReached);

				bool anyResultIsFalse = false;
				for (int i = 0; i < isSunLightReached.Count; ++i)
				{
					if (isSunLightReached[i] == false)
					{
						anyResultIsFalse = true;
						break;
					}
				}
				if (anyResultIsFalse)
					sunlightFactor = 0.0f;
				
				Debug.Log("Detecting sunlight: " + sunlightReached.ToString());
				Vector3 solarForce = CalculateSolarForce() * sunlightFactor;
				if (magneticModeActivated)
				{
					float actualElectricAcquired = this.part.RequestResource("ElectricCharge", magneticModeElectricConsumptionRate * surfaceArea * sunlightFactor); 
					Vector3 magnetForce = CalculateMagneticForce();
					magnetForce *= (actualElectricAcquired / magneticModeElectricConsumptionRate * surfaceArea);
					solarForce += magnetForce * sunlightFactor;
				}
				if(surfaceTransform != null)
					this.part.rigidbody.AddForceAtPosition(solarForce, surfaceTransform.position, ForceMode.Force);
				else
					this.part.rigidbody.AddForceAtPosition(solarForce, this.part.transform.position, ForceMode.Force);
				forceAcquired = solarForce.magnitude;
			}
		}

		private Vector3 CalculateSolarForce()
		{
			if (this.part != null)
			{
				Vector3 sunPosition = FlightGlobals.fetch.bodies[0].position;
				Vector3 ownPosition = this.part.transform.position;
				Vector3 normal = this.part.transform.up;
				if (surfaceTransform != null)
					normal = surfaceTransform.forward;
				Vector3 force = normal * Vector3.Dot((ownPosition - sunPosition).normalized, normal);
				return force * surfaceArea * reflectedPhotonRatio * thrustCoefficient * solarPowerCurve.Evaluate((sunPosition - ownPosition).magnitude);
			}
			else
				return Vector3.zero;
		}

		private Vector3 CalculateMagneticForce()
		{
			if (this.part != null)
			{
				Vector3 sunPosition = FlightGlobals.fetch.bodies[0].position;
				Vector3 ownPosition = this.part.transform.position;
				Vector3 normal = this.part.transform.up;
				if (surfaceTransform != null)
					normal = surfaceTransform.forward;
				Vector3 force = normal * Vector3.Dot((ownPosition - sunPosition).normalized, normal);
				return force * (1.0f - reflectedPhotonRatio) * thrustCoefficient * solarPowerCurve.Evaluate(Vector3.Distance(sunPosition, ownPosition)) * powerToThrustRatio * magneticModeElectricConsumptionRate;
			}
			else
				return Vector3.zero;
		}

		[KSPEvent(name = "ContextMenuActivateMagneticMode", guiActive = true, guiName = "Activate Magnetic Mode", active = true, category = "Solar Sail Mode")]
		public void ContextMenuActivateMagneticMode()
		{
			magneticModeActivated = true;
			Events["ContextMenuActivateMagneticMode"].active = false;
			Events["ContextMenuDeactivateMagneticMode"].active = true;
		}

		[KSPEvent(name = "ContextMenuDeactivateMagneticMode", guiActive = true, guiName = "Deactivate Magnetic Mode", active = false, category = "Solar Sail Mode")]
		public void ContextMenuDeactivateMagneticMode()
		{
			magneticModeActivated = false;
			Events["ContextMenuActivateMagneticMode"].active = true;
			Events["ContextMenuDeactivateMagneticMode"].active = false;
		}

		[KSPAction("Activate Magnetic Mode", actionGroup = KSPActionGroup.None)]
		public void ActionGroupActivateMagneticMode(KSPActionParam param)
		{
			ContextMenuActivateMagneticMode();
		}

		[KSPAction("Deactivate Magnetic Mode", actionGroup = KSPActionGroup.None)]
		public void ActionGroupDeactivateMagneticMode(KSPActionParam param)
		{
			ContextMenuDeactivateMagneticMode();
		}
	}
}
