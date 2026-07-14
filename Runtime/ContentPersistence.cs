using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ActionFit.Content
{
    /// <summary>Persists opaque, content-owned state JSON.</summary>
    public interface IContentStateStore
    {
        bool TryLoad(string contentId, out string json);
        void Save(string contentId, string json);
        void Delete(string contentId);
    }

    /// <summary>
    /// Optional durability boundary for stores whose normal Save is buffered.
    /// Content engines call Flush only for transitions that must survive an immediate restart.
    /// </summary>
    public interface IFlushableContentStateStore
    {
        void Flush();
    }

    /// <summary>Persists content state in two verified PlayerPrefs slots.</summary>
    public sealed class PlayerPrefsContentStateStore : IContentStateStore, IFlushableContentStateStore
    {
        public const string DefaultKeyPrefix = "com.actionfit.content-core.state";

        private const int CurrentSchemaVersion = 1;
        private const int SlotCount = 2;

        private readonly string _keyPrefix;

        public PlayerPrefsContentStateStore(string keyPrefix = DefaultKeyPrefix)
        {
            _keyPrefix = ValidateIdentifier(keyPrefix, nameof(keyPrefix));
        }

        /// <summary>Loads the payload from the highest valid revision.</summary>
        public bool TryLoad(string contentId, out string json)
        {
            contentId = ValidateIdentifier(contentId, nameof(contentId));
            SlotValue first = ReadSlot(contentId, 0);
            SlotValue second = ReadSlot(contentId, 1);

            if (!first.IsValid && !second.IsValid)
            {
                json = null;
                return false;
            }

            SlotValue newest = !second.IsValid || first.IsValid && first.Revision >= second.Revision
                ? first
                : second;
            json = newest.Payload;
            return true;
        }

        /// <summary>Writes the next revision while preserving the other valid slot.</summary>
        public void Save(string contentId, string json)
        {
            contentId = ValidateIdentifier(contentId, nameof(contentId));
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            SlotValue first = ReadSlot(contentId, 0);
            SlotValue second = ReadSlot(contentId, 1);
            long highestRevision = Math.Max(
                first.IsValid ? first.Revision : 0,
                second.IsValid ? second.Revision : 0);
            if (highestRevision == long.MaxValue)
            {
                throw new InvalidOperationException("Content state revision reached its maximum value.");
            }

            int targetSlot = SelectTargetSlot(first, second);
            var envelope = new StateEnvelope
            {
                schemaVersion = CurrentSchemaVersion,
                contentId = contentId,
                revision = highestRevision + 1,
                payload = json
            };
            envelope.sha256 = ComputeHash(envelope);

            PlayerPrefs.SetString(BuildSlotKey(contentId, targetSlot), JsonUtility.ToJson(envelope));
            Flush();
        }

        /// <summary>Deletes both persistence slots for the content ID.</summary>
        public void Delete(string contentId)
        {
            contentId = ValidateIdentifier(contentId, nameof(contentId));
            for (int slot = 0; slot < SlotCount; slot++)
            {
                PlayerPrefs.DeleteKey(BuildSlotKey(contentId, slot));
            }

            Flush();
        }

        public void Flush()
        {
            PlayerPrefs.Save();
        }

        internal string GetSlotKey(string contentId, int slot)
        {
            contentId = ValidateIdentifier(contentId, nameof(contentId));
            if (slot < 0 || slot >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }

            return BuildSlotKey(contentId, slot);
        }

        private SlotValue ReadSlot(string contentId, int slot)
        {
            string slotKey = BuildSlotKey(contentId, slot);
            if (!PlayerPrefs.HasKey(slotKey))
            {
                return default;
            }

            StateEnvelope envelope;
            try
            {
                envelope = JsonUtility.FromJson<StateEnvelope>(PlayerPrefs.GetString(slotKey));
            }
            catch (ArgumentException)
            {
                return default;
            }

            if (envelope == null
                || envelope.schemaVersion != CurrentSchemaVersion
                || !string.Equals(envelope.contentId, contentId, StringComparison.Ordinal)
                || envelope.revision <= 0
                || envelope.payload == null
                || string.IsNullOrEmpty(envelope.sha256))
            {
                return default;
            }

            string expectedHash = ComputeHash(envelope);
            if (!FixedTimeEquals(envelope.sha256, expectedHash))
            {
                return default;
            }

            return new SlotValue(envelope.revision, envelope.payload);
        }

        private static int SelectTargetSlot(SlotValue first, SlotValue second)
        {
            if (!first.IsValid)
            {
                return 0;
            }

            if (!second.IsValid)
            {
                return 1;
            }

            return first.Revision <= second.Revision ? 0 : 1;
        }

        private string BuildSlotKey(string contentId, int slot)
        {
            return $"{_keyPrefix}.{EncodeIdentifier(contentId)}.{slot}";
        }

        private static string EncodeIdentifier(string value)
        {
            var builder = new StringBuilder(value.Length * 4);
            foreach (char codeUnit in value)
            {
                builder.Append(((int)codeUnit).ToString("X4", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static string ComputeHash(StateEnvelope envelope)
        {
            var canonical = new StringBuilder();
            AppendHashField(canonical, envelope.schemaVersion.ToString(CultureInfo.InvariantCulture));
            AppendHashField(canonical, envelope.contentId);
            AppendHashField(canonical, envelope.revision.ToString(CultureInfo.InvariantCulture));
            AppendHashField(canonical, envelope.payload);

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
                var result = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                {
                    result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                }

                return result.ToString();
            }
        }

        private static void AppendHashField(StringBuilder builder, string value)
        {
            value = value ?? string.Empty;
            builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(value);
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            int difference = 0;
            for (int index = 0; index < left.Length; index++)
            {
                difference |= left[index] ^ right[index];
            }

            return difference == 0;
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
        private sealed class StateEnvelope
        {
            public int schemaVersion;
            public string contentId;
            public long revision;
            public string payload;
            public string sha256;
        }

        private readonly struct SlotValue
        {
            public SlotValue(long revision, string payload)
            {
                Revision = revision;
                Payload = payload;
                IsValid = true;
            }

            public long Revision { get; }
            public string Payload { get; }
            public bool IsValid { get; }
        }
    }
}
