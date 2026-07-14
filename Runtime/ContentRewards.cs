using System;
using System.Collections.Generic;
using UnityEngine;

namespace ActionFit.Content
{
    /// <summary>Describes a project-neutral reward balance mutation.</summary>
    public sealed class ContentReward
    {
        public ContentReward(string rewardId, long amount)
        {
            if (string.IsNullOrWhiteSpace(rewardId))
            {
                throw new ArgumentException("Reward ID must not be empty or whitespace.", nameof(rewardId));
            }

            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Reward amount must be positive.");
            }

            RewardId = rewardId;
            Amount = amount;
        }

        public string RewardId { get; }
        public long Amount { get; }
    }

    /// <summary>Grants rewards once for each stable transaction ID.</summary>
    public interface IContentRewardService
    {
        /// <summary>False when the host has not connected a safe reward grant implementation.</summary>
        bool IsAvailable { get; }
        bool HasGranted(string transactionId);
        bool GrantOnce(string transactionId, IReadOnlyList<ContentReward> rewards);
    }

    /// <summary>Stores idempotent local reward grants in one PlayerPrefs ledger.</summary>
    public sealed class PlayerPrefsContentRewardService : IContentRewardService
    {
        public const string DefaultLedgerKey = "com.actionfit.content-core.rewards";

        private const int CurrentSchemaVersion = 1;

        private readonly string _ledgerKey;

        public PlayerPrefsContentRewardService(string ledgerKey = DefaultLedgerKey)
        {
            _ledgerKey = ValidateIdentifier(ledgerKey, nameof(ledgerKey));
        }

        public bool IsAvailable => true;

        /// <summary>Returns whether the transaction has already been durably granted.</summary>
        public bool HasGranted(string transactionId)
        {
            transactionId = ValidateIdentifier(transactionId, nameof(transactionId));
            RewardLedger ledger = LoadLedger();
            return ledger.grantedTransactionIds.Contains(transactionId);
        }

        /// <summary>Applies all rewards only when the transaction ID is new.</summary>
        public bool GrantOnce(string transactionId, IReadOnlyList<ContentReward> rewards)
        {
            transactionId = ValidateIdentifier(transactionId, nameof(transactionId));
            Dictionary<string, long> requestedBalances = ValidateAndSumRewards(rewards);
            RewardLedger ledger = LoadLedger();
            if (ledger.grantedTransactionIds.Contains(transactionId))
            {
                return false;
            }

            var balances = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (RewardBalanceEntry entry in ledger.balances)
            {
                balances.Add(entry.rewardId, entry.amount);
            }

            foreach (KeyValuePair<string, long> reward in requestedBalances)
            {
                balances.TryGetValue(reward.Key, out long currentBalance);
                balances[reward.Key] = checked(currentBalance + reward.Value);
            }

            ledger.grantedTransactionIds.Add(transactionId);
            ledger.balances.Clear();
            var rewardIds = new List<string>(balances.Keys);
            rewardIds.Sort(StringComparer.Ordinal);
            foreach (string rewardId in rewardIds)
            {
                ledger.balances.Add(new RewardBalanceEntry
                {
                    rewardId = rewardId,
                    amount = balances[rewardId]
                });
            }

            SaveLedger(ledger);
            return true;
        }

        /// <summary>Returns the local default ledger balance for a reward ID.</summary>
        public long GetBalance(string rewardId)
        {
            rewardId = ValidateIdentifier(rewardId, nameof(rewardId));
            RewardLedger ledger = LoadLedger();
            foreach (RewardBalanceEntry entry in ledger.balances)
            {
                if (string.Equals(entry.rewardId, rewardId, StringComparison.Ordinal))
                {
                    return entry.amount;
                }
            }

            return 0;
        }

        private RewardLedger LoadLedger()
        {
            if (!PlayerPrefs.HasKey(_ledgerKey))
            {
                return RewardLedger.Create();
            }

            RewardLedger ledger;
            try
            {
                ledger = JsonUtility.FromJson<RewardLedger>(PlayerPrefs.GetString(_ledgerKey));
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException("The content reward ledger is corrupted.", exception);
            }

            ValidateLedger(ledger);
            return ledger;
        }

        private void SaveLedger(RewardLedger ledger)
        {
            PlayerPrefs.SetString(_ledgerKey, JsonUtility.ToJson(ledger));
            PlayerPrefs.Save();
        }

        private static void ValidateLedger(RewardLedger ledger)
        {
            if (ledger == null
                || ledger.schemaVersion != CurrentSchemaVersion
                || ledger.grantedTransactionIds == null
                || ledger.balances == null)
            {
                throw new InvalidOperationException("The content reward ledger is corrupted.");
            }

            var transactionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string transactionId in ledger.grantedTransactionIds)
            {
                if (string.IsNullOrWhiteSpace(transactionId) || !transactionIds.Add(transactionId))
                {
                    throw new InvalidOperationException("The content reward ledger contains an invalid transaction.");
                }
            }

            var rewardIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (RewardBalanceEntry entry in ledger.balances)
            {
                if (entry == null
                    || string.IsNullOrWhiteSpace(entry.rewardId)
                    || entry.amount <= 0
                    || !rewardIds.Add(entry.rewardId))
                {
                    throw new InvalidOperationException("The content reward ledger contains an invalid balance.");
                }
            }
        }

        private static Dictionary<string, long> ValidateAndSumRewards(IReadOnlyList<ContentReward> rewards)
        {
            if (rewards == null)
            {
                throw new ArgumentNullException(nameof(rewards));
            }

            if (rewards.Count == 0)
            {
                throw new ArgumentException("At least one reward is required.", nameof(rewards));
            }

            var totals = new Dictionary<string, long>(StringComparer.Ordinal);
            for (int index = 0; index < rewards.Count; index++)
            {
                ContentReward reward = rewards[index];
                if (reward == null)
                {
                    throw new ArgumentException("Rewards must not contain null entries.", nameof(rewards));
                }

                totals.TryGetValue(reward.RewardId, out long currentAmount);
                totals[reward.RewardId] = checked(currentAmount + reward.Amount);
            }

            return totals;
        }

        private static string ValidateIdentifier(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value must not be empty or whitespace.", parameterName);
            }

            return value;
        }

        [Serializable]
        private sealed class RewardLedger
        {
            public int schemaVersion;
            public List<string> grantedTransactionIds;
            public List<RewardBalanceEntry> balances;

            public static RewardLedger Create()
            {
                return new RewardLedger
                {
                    schemaVersion = CurrentSchemaVersion,
                    grantedTransactionIds = new List<string>(),
                    balances = new List<RewardBalanceEntry>()
                };
            }
        }

        [Serializable]
        private sealed class RewardBalanceEntry
        {
            public string rewardId;
            public long amount;
        }
    }
}
