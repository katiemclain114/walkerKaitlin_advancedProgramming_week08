using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;

public enum AgentState {Normal, Dashing, Stunning}
public class MinerAgent : Agent
{
	GameGroup gameGroup;

	[HideInInspector]
	public Rigidbody rB;
	Vector3 originalPos;
	Quaternion originalRota;
	string originalTag;
	Color originalColor;
	[SerializeField]
	Color dashColor;
	[SerializeField]
	Color stunColor;
	[SerializeField] 
	private Color shieldColor;
	[SerializeField]
	Renderer cubeRenderer;
	[SerializeField]
	float rotateSpeed = 3;
	[SerializeField]
	float speed = 100;
	[SerializeField]
	float maxSpeed = 3;
	[SerializeField]
	float maxDashSpeed = 6;

	AgentState currentAgentState = AgentState.Normal;
	const int NUM_AGENT_STATES = (int)AgentState.Stunning + 1;
	float dashingTimer = 0;
	float dashingTimerTotal = .3f;

	bool dashCDReady = true;
	float dashCDTimer = 0;
	float dashCDTimerTotal = 2;

	float stunningTimer = 0;
	float stunningTimerTotal = 10f;

	int teamID = -1;
	int teamID_Opponent = -1;

	private int shieldGoal = 2;
	private int goldPickupAmt = 0;
	private bool shieldEarned = false;

	public override void Initialize()
	{
		rB = GetComponent<Rigidbody>();
		gameGroup = GetComponentInParent<GameGroup>();
		originalPos = transform.localPosition;
		originalRota = transform.localRotation;
		originalTag = gameObject.tag;
		originalColor = cubeRenderer.material.GetColor("_Color");
		teamID = GetComponent<BehaviorParameters>().TeamId;

		if (teamID == 1)
		{
			teamID_Opponent = 0;
		}
		else
		{
			teamID_Opponent = 1;
		}
	}
	public override void OnEpisodeBegin()
	{
		transform.localPosition = originalPos;
		transform.localRotation = originalRota;
		gameObject.tag = originalTag;
		cubeRenderer.material.SetColor("_Color", originalColor);
		rB.velocity = Vector3.zero;

		currentAgentState = AgentState.Normal;
		dashingTimer = 0;
		dashCDTimer = 0;
		dashCDReady = true;
		stunningTimer = 0;

	}
	public override void CollectObservations(VectorSensor sensor)
	{
		sensor.AddObservation(rB.velocity);
		sensor.AddObservation(transform.forward);

		sensor.AddObservation(stunningTimer / stunningTimerTotal);
		sensor.AddObservation(dashingTimer / dashingTimerTotal);
		sensor.AddObservation(dashCDTimer / dashCDTimerTotal);

		sensor.AddObservation(teamID);
		float scoreDifference = 0;
		if (teamID == 0)
		{
			scoreDifference = (float)gameGroup.blueScore - gameGroup.redScore;
		}
		else
		{
			scoreDifference = (float)gameGroup.redScore - gameGroup.blueScore;
		}
		sensor.AddObservation(Mathf.Clamp(scoreDifference / 10, -1, 1));
		sensor.AddObservation((float)gameGroup.currentEnvironmentStep / gameGroup.MaxEnvironmentSteps);

		sensor.AddOneHotObservation((int)currentAgentState, NUM_AGENT_STATES);
	}

	public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
	{
		actionMask.SetActionEnabled(0, 1, currentAgentState != AgentState.Stunning);
		actionMask.SetActionEnabled(0, 2, currentAgentState != AgentState.Stunning);
		actionMask.SetActionEnabled(1, 1, currentAgentState != AgentState.Stunning);
		actionMask.SetActionEnabled(1, 2, currentAgentState != AgentState.Stunning);
		actionMask.SetActionEnabled(2, 1, dashCDReady && currentAgentState != AgentState.Stunning);
	}
	public override void OnActionReceived(ActionBuffers actions)
	{
		//Actions: 0=Do nothing 1=Up/Right 2=Down/Left
		float rotateValue = 0;
		float forwardValue = 0;
		var discreteActions = actions.DiscreteActions;

		int rotateAction = discreteActions[0];
		int forwardAction = discreteActions[1];
		int dashAction = discreteActions[2];

		switch(rotateAction)
		{
			case 1:
				rotateValue = 1;
				break;
			case 2:
				rotateValue = -1;
				break;
		}

		switch (forwardAction)
		{
			case 1:
				forwardValue = 1;
				break;
			case 2:
				forwardValue = -1;
				break;
		}

		Vector3 force = transform.forward * forwardValue * (speed + dashAction * 2);

		if (dashAction == 1 && forwardAction == 1)
		{
			cubeRenderer.material.SetColor("_Color", dashColor);
			dashCDReady = false;
			currentAgentState = AgentState.Dashing;
			dashingTimer = dashingTimerTotal;
			AddReward(-.01f);
		}

		if (currentAgentState == AgentState.Dashing)
		{
			force = force * 1.5f;
		}

		rB.AddForce(force, ForceMode.VelocityChange);
		transform.Rotate(transform.up * rotateValue, rotateSpeed);

	}
	public override void Heuristic(in ActionBuffers actionsOut)
	{

		float rotateValue = Input.GetAxis("Horizontal");
		float forwardValue = Input.GetAxis("Vertical");

		int rotateAction = 0;
		int forwardAction = 0;

		if (rotateValue > 0.01f)
		{
			rotateAction = 1;
		}
		else if (rotateValue < -0.01f)
		{
			rotateAction = 2;
		}

		if (forwardValue > 0.01f)
		{
			forwardAction = 1;
		}
		else if (forwardValue < -0.01f)
		{
			forwardAction = 2;
		}

		if (currentAgentState != AgentState.Stunning)
		{
			var discreteActions = actionsOut.DiscreteActions;
			discreteActions[0] = rotateAction;
			discreteActions[1] = forwardAction;

			if (Input.GetKey(KeyCode.Space) && dashCDReady)
			{
				discreteActions[2] = 1;
			}
		}
	}
	private void FixedUpdate()
	{
		if (currentAgentState != AgentState.Dashing)
		{
			if (rB.velocity.magnitude > maxSpeed)
			{
				rB.velocity = rB.velocity.normalized * maxSpeed;
			}

			if (!dashCDReady)
			{
				if (dashCDTimer > 0)
				{
					dashCDTimer -= Time.fixedDeltaTime;
				}
				else
				{
					dashCDTimer = 0;
					dashCDReady = true;
				}
			}


			if (currentAgentState == AgentState.Stunning)
			{
				if (stunningTimer > 0)
				{
					stunningTimer -= Time.fixedDeltaTime;
				}
				else
				{
					stunningTimer = 0;
					currentAgentState = AgentState.Normal;
					gameObject.tag = originalTag;
					cubeRenderer.material.SetColor("_Color", shieldEarned ? shieldColor : originalColor);
				}
			}
		}
		else if(currentAgentState == AgentState.Dashing)
		{
			if (rB.velocity.magnitude > maxDashSpeed)
			{
				rB.velocity = rB.velocity.normalized * maxDashSpeed;
			}

			if (dashingTimer > 0)
			{
				dashingTimer -= Time.fixedDeltaTime;
			}
			else
			{
				dashingTimer = 0;
				dashCDTimer = dashCDTimerTotal;
				currentAgentState = AgentState.Normal;
				cubeRenderer.material.SetColor("_Color", shieldEarned ? shieldColor : originalColor);
			}
		}
	}


	private void OnTriggerEnter(Collider other)
	{
		if (!other.tag.Contains("Gold")) return;

		AddReward(.2f);
		if (currentAgentState == AgentState.Dashing)
		{
			AddReward(.02f);
		}
		gameGroup.event_GoalScored.Invoke(teamID);

		gameGroup.DestroyGold(other.gameObject.transform);

		if (shieldEarned) return;
		if (goldPickupAmt < shieldGoal)
		{
			goldPickupAmt++;
		}
		else
		{
			shieldEarned = true;
			cubeRenderer.material.SetColor("_Color", shieldColor);
		}
	}
	private void OnCollisionStay(Collision other)
	{
		//"GameAgent_0" or "GameAgent_1" means blue & red team
		if (other.gameObject.tag.Contains("GameAgent_" + teamID_Opponent) && currentAgentState == AgentState.Dashing)
		{
			MinerAgent agent = other.gameObject.GetComponent<MinerAgent>();
			if (agent.HitCheck())
			{
				AddReward(.5f);
			}
			else
			{
				AddReward(.02f);
			}
		}
	}

	public bool HitCheck()
	{
		if (shieldEarned) return false;
		if (currentAgentState != AgentState.Dashing && currentAgentState != AgentState.Stunning)
		{
			currentAgentState = AgentState.Stunning;
			stunningTimer = stunningTimerTotal;
			gameObject.tag = "Stun";
			cubeRenderer.material.SetColor("_Color", stunColor);
			return true;
		}
		return false;
	}
}
