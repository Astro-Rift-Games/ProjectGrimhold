using NUnit.Framework;
using Spawning;
using UnityEngine;
using System.Collections.Generic;

namespace Grimhold.Tests.Networking
{
    [TestFixture]
    public class ReinforcementSpawnPlannerTests
    {
        private ReinforcementPolicy CreateBasePolicy()
        {
            return new ReinforcementPolicy
            {
                PopulationBudget = 5,
                EvaluationIntervalSeconds = 10f,
                MinSecondsBetweenSpawns = 2f,
                MaxSpawnsPerAttempt = 3,
                MinDistanceToPlayer = 10f
            };
        }

        [Test]
        public void Plan_WhenBudgetZero_ReturnsDisabled()
        {
            var policy = CreateBasePolicy();
            policy.PopulationBudget = 0;
            
            var plan = ReinforcementSpawnPlanner.Plan(policy, 5, 5, true, true);
            
            Assert.AreEqual(ReinforcementRejection.Disabled, plan.Rejection);
            Assert.AreEqual(0, plan.Count);
        }

        [Test]
        public void Plan_WhenIntervalNotElapsed_ReturnsIntervalNotElapsed()
        {
            var policy = CreateBasePolicy();
            
            var plan = ReinforcementSpawnPlanner.Plan(policy, 5, 5, false, true);
            
            Assert.AreEqual(ReinforcementRejection.IntervalNotElapsed, plan.Rejection);
            Assert.AreEqual(0, plan.Count);
        }

        [Test]
        public void Plan_WhenCapacityBudgetZero_ReturnsPopulationBudgetExhausted()
        {
            var policy = CreateBasePolicy();
            
            var plan = ReinforcementSpawnPlanner.Plan(policy, 0, 5, true, true);
            
            Assert.AreEqual(ReinforcementRejection.PopulationBudgetExhausted, plan.Rejection);
            Assert.AreEqual(0, plan.Count);
        }

        [Test]
        public void Plan_WhenCapacityGlobalZero_ReturnsGlobalCapReached()
        {
            var policy = CreateBasePolicy();
            
            var plan = ReinforcementSpawnPlanner.Plan(policy, 5, 0, true, true);
            
            Assert.AreEqual(ReinforcementRejection.GlobalCapReached, plan.Rejection);
            Assert.AreEqual(0, plan.Count);
        }

        [Test]
        public void Plan_WhenNoValidPoints_ReturnsNoReinforcementPoints()
        {
            var policy = CreateBasePolicy();
            
            var plan = ReinforcementSpawnPlanner.Plan(policy, 5, 5, true, false);
            
            Assert.AreEqual(ReinforcementRejection.NoReinforcementPoints, plan.Rejection);
            Assert.AreEqual(0, plan.Count);
        }

        [Test]
        public void Plan_WhenAllConditionsMet_ClampsToMaxPerAttempt()
        {
            var policy = CreateBasePolicy();
            policy.MaxSpawnsPerAttempt = 2;
            
            var plan = ReinforcementSpawnPlanner.Plan(policy, 5, 5, true, true);
            
            Assert.AreEqual(ReinforcementRejection.None, plan.Rejection);
            Assert.AreEqual(2, plan.Count);
        }

        [Test]
        public void Plan_WhenCapacityBudgetIsLower_ClampsToCapacityBudget()
        {
            var policy = CreateBasePolicy();
            policy.MaxSpawnsPerAttempt = 5;
            
            var plan = ReinforcementSpawnPlanner.Plan(policy, 2, 5, true, true);
            
            Assert.AreEqual(ReinforcementRejection.None, plan.Rejection);
            Assert.AreEqual(2, plan.Count);
        }

        [Test]
        public void Plan_WhenCapacityGlobalIsLower_ClampsToCapacityGlobal()
        {
            var policy = CreateBasePolicy();
            policy.MaxSpawnsPerAttempt = 5;
            
            var plan = ReinforcementSpawnPlanner.Plan(policy, 5, 1, true, true);
            
            Assert.AreEqual(ReinforcementRejection.None, plan.Rejection);
            Assert.AreEqual(1, plan.Count);
        }

        [Test]
        public void EvaluateAndSelectPoint_WhenAllPointsTooClose_ReturnsNull()
        {
            var points = new List<Transform>();
            var go1 = new GameObject("p1"); go1.transform.position = Vector3.zero;
            var go2 = new GameObject("p2"); go2.transform.position = Vector3.right * 2f;
            points.Add(go1.transform);
            points.Add(go2.transform);

            var playerPositions = new List<Vector3> { Vector3.zero };
            var buffer = new List<Transform>();

            // Require min distance 5
            var selected = ReinforcementSpawnPlanner.EvaluateAndSelectPoint(points, playerPositions, 5f, buffer);
            
            Assert.IsNull(selected);
            Assert.AreEqual(0, buffer.Count);

            Object.DestroyImmediate(go1);
            Object.DestroyImmediate(go2);
        }

        [Test]
        public void EvaluateAndSelectPoint_WhenOnePointNearPlayer_SelectsFarPoint()
        {
            var points = new List<Transform>();
            var nearGo = new GameObject("near"); nearGo.transform.position = Vector3.right * 2f;
            var farGo = new GameObject("far"); farGo.transform.position = Vector3.right * 20f;
            points.Add(nearGo.transform);
            points.Add(farGo.transform);

            var playerPositions = new List<Vector3> { Vector3.zero };
            var buffer = new List<Transform>();

            var selected = ReinforcementSpawnPlanner.EvaluateAndSelectPoint(points, playerPositions, 5f, buffer);

            Assert.AreSame(farGo.transform, selected);
            Assert.AreEqual(1, buffer.Count);

            Object.DestroyImmediate(nearGo);
            Object.DestroyImmediate(farGo);
        }
    }
}
