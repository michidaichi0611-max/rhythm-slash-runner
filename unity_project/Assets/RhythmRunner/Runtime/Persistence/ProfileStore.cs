using System;
using System.IO;
using UnityEngine;
using RhythmSlashRunner.Core;

namespace RhythmSlashRunner.Persistence
{
    public enum PurchaseResult { Bought, AlreadyOwned, NotEnoughCoins, NotForSale, SaveFailed }

    /// <summary>
    /// Local profile with temp-file → verify → replace → backup saves. Rewards and purchases are applied to a copy and
    /// only committed to the live profile after the save succeeded, so balance and ownership never update separately.
    /// </summary>
    public static class ProfileStore
    {
        /// <summary>Overridable for tests.</summary>
        public static string Directory = null;

        static string Root { get { return Directory ?? Application.persistentDataPath; } }
        static string SavePath { get { return Path.Combine(Root, "rhythm_slash_profile.json"); } }
        static string BackupPath { get { return SavePath + ".bak"; } }

        public static PlayerProfile Load()
        {
            PlayerProfile loaded = TryRead(SavePath) ?? TryRead(BackupPath) ?? new PlayerProfile();
            loaded.Migrate();
            return loaded;
        }

        public static void Save(PlayerProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            System.IO.Directory.CreateDirectory(Root);
            string temp = SavePath + ".tmp";
            profile.generation++;
            File.WriteAllText(temp, JsonUtility.ToJson(profile, true));
            PlayerProfile check = TryRead(temp);
            if (check == null || check.generation != profile.generation) throw new IOException("Profile verification failed.");
            if (File.Exists(SavePath)) File.Copy(SavePath, BackupPath, true);
            if (File.Exists(SavePath)) File.Delete(SavePath);
            File.Move(temp, SavePath);
        }

        /// <summary>Returns coins granted (0 when this runId was already applied). Throws if saving fails.</summary>
        public static int ApplyRunReward(PlayerProfile profile, string runId, RunSnapshot snapshot)
        {
            if (profile.appliedRewardRunIds.Contains(runId)) return 0;
            PlayerProfile next = profile.Clone();
            int reward = RunEngine.CoinsForRun(snapshot) + NewBestBonus(profile, snapshot);
            next.coinBalance += reward;
            next.bestDistance = Math.Max(next.bestDistance, snapshot.Distance);
            next.bestScore = Math.Max(next.bestScore, snapshot.Score);
            next.appliedRewardRunIds.Add(runId);
            while (next.appliedRewardRunIds.Count > 100) next.appliedRewardRunIds.RemoveAt(0);
            Save(next);
            CopyInto(next, profile);
            return reward;
        }

        /// <summary>Extra coins whenever the best distance is beaten (including the very first record).</summary>
        public static int NewBestBonus(PlayerProfile profile, RunSnapshot snapshot)
        {
            return snapshot.Distance > 0 && snapshot.Distance > profile.bestDistance ? RunConfig.Default.NewBestBonusCoins : 0;
        }

        public static PurchaseResult TryPurchase(PlayerProfile profile, CatalogItem item)
        {
            if (item == null || !item.Playable) return PurchaseResult.NotForSale;
            if (profile.Owns(item.id)) return PurchaseResult.AlreadyOwned;
            if (profile.coinBalance < item.price) return PurchaseResult.NotEnoughCoins;
            PlayerProfile next = profile.Clone();
            next.coinBalance -= item.price;
            next.ownedIds.Add(item.id);
            try { Save(next); }
            catch (Exception e) { Debug.LogException(e); return PurchaseResult.SaveFailed; }
            CopyInto(next, profile);
            return PurchaseResult.Bought;
        }

        /// <summary>Saves settings/equipment changes; returns false (and keeps playing) when storage fails.</summary>
        public static bool TrySave(PlayerProfile profile)
        {
            try { Save(profile); return true; }
            catch (Exception e) { Debug.LogException(e); return false; }
        }

        static void CopyInto(PlayerProfile source, PlayerProfile target) { JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), target); }

        static PlayerProfile TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                PlayerProfile profile = JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(path));
                return profile != null && profile.schemaVersion == 1 ? profile : null;
            }
            catch { return null; }
        }
    }
}
