using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ActionFit.Content.Tests
{
    public class ContentCoreTests
    {
        private const string ContentId = "test-content";

        private string _statePrefix;
        private string _ledgerKey;
        private PlayerPrefsContentStateStore _stateStore;

        [SetUp]
        public void SetUp()
        {
            string uniqueId = Guid.NewGuid().ToString("N");
            _statePrefix = $"ActionFit.Content.Tests.State.{uniqueId}";
            _ledgerKey = $"ActionFit.Content.Tests.Rewards.{uniqueId}";
            _stateStore = new PlayerPrefsContentStateStore(_statePrefix);
        }

        [TearDown]
        public void TearDown()
        {
            if (_stateStore != null)
            {
                _stateStore.Delete(ContentId);
            }

            if (!string.IsNullOrEmpty(_ledgerKey))
            {
                PlayerPrefs.DeleteKey(_ledgerKey);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void StateStore_LoadsNewestValidSlot()
        {
            _stateStore.Save(ContentId, "{\"revision\":1}");
            _stateStore.Save(ContentId, "{\"revision\":2}");

            bool loaded = _stateStore.TryLoad(ContentId, out string json);

            Assert.That(loaded, Is.True);
            Assert.That(json, Is.EqualTo("{\"revision\":2}"));
        }

        [Test]
        public void StateStore_CorruptedNewestSlotFallsBackToPreviousSlot()
        {
            _stateStore.Save(ContentId, "previous");
            _stateStore.Save(ContentId, "newest");
            PlayerPrefs.SetString(_stateStore.GetSlotKey(ContentId, 1), "{broken-json");
            PlayerPrefs.Save();

            bool loaded = _stateStore.TryLoad(ContentId, out string json);

            Assert.That(loaded, Is.True);
            Assert.That(json, Is.EqualTo("previous"));
        }

        [Test]
        public void StateStore_DeleteRemovesBothSlots()
        {
            _stateStore.Save(ContentId, "first");
            _stateStore.Save(ContentId, "second");

            _stateStore.Delete(ContentId);

            Assert.That(_stateStore.TryLoad(ContentId, out string json), Is.False);
            Assert.That(json, Is.Null);
            Assert.That(PlayerPrefs.HasKey(_stateStore.GetSlotKey(ContentId, 0)), Is.False);
            Assert.That(PlayerPrefs.HasKey(_stateStore.GetSlotKey(ContentId, 1)), Is.False);
        }

        [Test]
        public void RewardService_DuplicateTransactionIsIdempotent()
        {
            var service = new PlayerPrefsContentRewardService(_ledgerKey);
            var rewards = new List<ContentReward> { new ContentReward("coin", 100) };

            bool firstGrant = service.GrantOnce("transaction-1", rewards);
            var restartedService = new PlayerPrefsContentRewardService(_ledgerKey);
            bool duplicateGrant = restartedService.GrantOnce("transaction-1", rewards);

            Assert.That(firstGrant, Is.True);
            Assert.That(duplicateGrant, Is.False);
            Assert.That(restartedService.HasGranted("transaction-1"), Is.True);
            Assert.That(restartedService.GetBalance("coin"), Is.EqualTo(100));
        }

        [Test]
        public void RewardService_MalformedExistingLedgerThrowsInsteadOfRegranting()
        {
            PlayerPrefs.SetString(_ledgerKey, "{broken-json");
            PlayerPrefs.Save();
            var service = new PlayerPrefsContentRewardService(_ledgerKey);

            Assert.Throws<InvalidOperationException>(() => service.GrantOnce(
                "transaction-1",
                new List<ContentReward> { new ContentReward("coin", 100) }));
        }

        [Test]
        public void RewardService_SumsBalancesAcrossRewardsAndTransactions()
        {
            var service = new PlayerPrefsContentRewardService(_ledgerKey);

            service.GrantOnce(
                "transaction-1",
                new List<ContentReward>
                {
                    new ContentReward("coin", 40),
                    new ContentReward("coin", 60),
                    new ContentReward("gem", 2)
                });
            service.GrantOnce(
                "transaction-2",
                new List<ContentReward>
                {
                    new ContentReward("coin", 25),
                    new ContentReward("gem", 3)
                });

            Assert.That(service.GetBalance("coin"), Is.EqualTo(125));
            Assert.That(service.GetBalance("gem"), Is.EqualTo(5));
        }
    }
}
