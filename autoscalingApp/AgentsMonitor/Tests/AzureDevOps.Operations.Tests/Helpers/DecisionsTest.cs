﻿using AzureDevOps.Operations.Helpers;
using AzureDevOps.Operations.Models;
using AzureDevOps.Operations.Tests.Classes;
using AzureDevOps.Operations.Tests.Data;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.Management.Compute.Fluent;

namespace AzureDevOps.Operations.Tests.Helpers
{
    public class DecisionsTest
    {
        [TestCase(2, 1, true, Description = "There is 2 jobs, and 1 agent - we shall upscale")]
        [TestCase(1, 2, false, Description = "There is 1 job, and 2 agent - we shall downscale")]
        public static void WhatToDoWithAgents(int jobsCount, int agentsCount, bool expectedResult)
        {
            var operation = Decisions.AddMoreAgents(jobsCount, agentsCount);

            Assert.AreEqual(operation, expectedResult);
        }

        [TestCase(3, 3, 7, 0, Description = "There is 3 jobs and 3 agents - do not need to add anything")]
        [TestCase(5, 3, 7, 2, Description = "There is 5 jobs and 3 agents - need 2 more agents")]
        [TestCase(1, 3, 7, 2, Description = "There is 1 job and 3 agents - could deprovision 2 agents")]
        [TestCase(10, 7, 7, 0, Description = "There is 10 jobs and 7 agents, with 7 agents max - could not do anything")]
        [TestCase(7, 7, 7, 0, Description = "There is 7 jobs and 7 agents, with 7 agents max - should not do anything")]
        public static void AmountOfAgents(int jobsCount, int agentsCount, int maxAgentsCount, int expectedAmount)
        {
            var amount = Decisions.HowMuchAgents(jobsCount, agentsCount, maxAgentsCount);

            Assert.AreEqual(amount, expectedAmount);
        }

        [Test]
        public static void TestInstanceIdRetrieval_agent_is_there()
        {
            var testValid = new ScaleSetVirtualMachineStripped
            {
                VmName = "Agent",
                VmInstanceId = "205",
                VmInstanceState = PowerState.Running
            };

            var testArray = new ScaleSetVirtualMachineStripped[1];
            testArray[0] = testValid;

            var vmScaleSetData = GetTestData(10, testArray);
            var instanceIds = GetInstanceIds(vmScaleSetData);
            Assert.IsTrue(instanceIds.Length.Equals(10));
            Assert.IsFalse(instanceIds[0].Equals(testValid.VmInstanceId));
        }

        [Test]
        public static void TestInstanceIdRetrieval_agents_is_there()
        {
            //this test ensures that we would not deallocate VMs, which have running jobs
            var testArray = new ScaleSetVirtualMachineStripped[3];
            var testValid = new ScaleSetVirtualMachineStripped
            {
                VmName = "Agent",
                VmInstanceId = "205",
                VmInstanceState = PowerState.Running
            };

            testArray[0] = testValid;
            testValid = new ScaleSetVirtualMachineStripped
            {
                VmName = "Agent1",
                VmInstanceId = "2052",
                VmInstanceState = PowerState.Running
            };

            testArray[1] = testValid;
            testValid = new ScaleSetVirtualMachineStripped
            {
                VmName = "Agent2",
                VmInstanceId = "20522",
                VmInstanceState = PowerState.Running
            };

            testArray[2] = testValid;

            var vmScaleSetData = GetTestData(10, testArray);
            var instanceIds = GetInstanceIds(vmScaleSetData, TestsConstants.TestPoolId, TestsConstants.Json3JobIsRunning);
            Assert.IsTrue(instanceIds.Length.Equals(10));
            Assert.IsFalse(instanceIds[0].Equals(testArray[0].VmInstanceId));
        }

        [Test]
        public static void TestInstanceIdRetrieval_agent_is_not_there()
        {
            //this test describes situation, when we have something running in the pool, but not on our agents :); really weird issue
            var vmScaleSetData = GetTestData(10);

            var instanceIds = GetInstanceIds(vmScaleSetData);
            Assert.IsTrue(instanceIds.Length.Equals(10));
        }

        [Test]
        public static void TestInstanceIdRetrieval_no_jobs_retrieved()
        {
            //this test ensures that when there is no jobs running - we can deallocate all VMs
            var vmScaleSetData = GetTestData(10);

            var instanceIds = GetInstanceIds(vmScaleSetData, 1);
            Assert.IsTrue(instanceIds.Length.Equals(10));
        }
        /// <summary>
        /// Where there is no running instances - nothing could be deallocated
        /// </summary>
        [Test]
        public static void TestInstanceIdRetrieval_no_running_vms_there()
        {
            var vmScaleSetData = GetTestData(10);
            foreach (var t in vmScaleSetData)
            {
                t.VmInstanceState = PowerState.Deallocated;
            }
            var instanceIds = GetInstanceIds(vmScaleSetData, 1);
            Assert.IsTrue(instanceIds.Length.Equals(0));
        }

        private static string[] GetInstanceIds(IEnumerable<ScaleSetVirtualMachineStripped> vmScaleSetData, int poolId = TestsConstants.TestPoolId, string jsonData = TestsConstants.Json1JobIsRunning)
        {
            var dataRetriever = RetrieveTests.CreateRetriever(jsonData);
            return Decisions.CollectInstanceIdsToDeallocate(vmScaleSetData.Where(vm => vm.VmInstanceState.Equals(PowerState.Running)),
                dataRetriever.GetRuningJobs(poolId));
        }

        /// <summary>
        /// Generates stripped VMSS list to work with; allows to generate to amount which is needed and add custom data to the collection
        /// </summary>
        /// <param name="testListSize"></param>
        /// <param name="addedData"></param>
        /// <returns></returns>
        private static List<ScaleSetVirtualMachineStripped> GetTestData(int testListSize, ScaleSetVirtualMachineStripped[] addedData = null)
        {
            var vmScaleSetData = new List<ScaleSetVirtualMachineStripped>();
            if (addedData != null)
            {
                vmScaleSetData.AddRange(addedData);
            }

            for (var counter = 0; counter < testListSize; counter++)
            {
                vmScaleSetData.Add(new ScaleSetVirtualMachineStripped
                {
                    VmName = $"vm{counter}",
                    VmInstanceId = $"{counter}",
                    VmInstanceState = PowerState.Running
                });
            }

            return vmScaleSetData;

        }
    }
}