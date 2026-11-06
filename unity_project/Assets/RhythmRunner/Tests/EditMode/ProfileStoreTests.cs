using System.IO;
using NUnit.Framework;
using RhythmSlashRunner.Core;
using RhythmSlashRunner.Persistence;

namespace RhythmSlashRunner.Tests
{
    public sealed class ProfileStoreTests
    {
        string dir;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "rsr_profile_tests_" + System.Guid.NewGuid().ToString("N"));
            ProfileStore.Directory = dir;
        }

        [TearDown]
        public void TearDown()
        {
            ProfileStore.Directory = null;
            if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true);
        }

        static RunSnapshot Snapshot(int distance, int perfect) { return new RunSnapshot { Distance = distance, Perfect = perfect, CoinUnits = perfect, Score = perfect * 1000 }; }

        [Test]
        public void RewardIsAppliedOncePerRunIdAndSurvivesReload()
        {
            var profile = new PlayerProfile();
            int first = ProfileStore.ApplyRunReward(profile, "run-1", Snapshot(250, 9));
            int again = ProfileStore.ApplyRunReward(profile, "run-1", Snapshot(250, 9));
            Assert.AreEqual(3 + 15 + 10, first); // 9 units / 3 + 3 coins per 50 m + first record bonus
            Assert.AreEqual(0, again);
            PlayerProfile loaded = ProfileStore.Load();
            Assert.AreEqual(28, loaded.coinBalance);
            Assert.AreEqual(250, loaded.bestDistance);
            Assert.AreEqual(0, ProfileStore.ApplyRunReward(loaded, "run-1", Snapshot(250, 9)));
        }

        [Test]
        public void BeatingAnExistingBestAddsTheNewBestBonusOnce()
        {
            var profile = new PlayerProfile { bestDistance = 100 };
            int reward = ProfileStore.ApplyRunReward(profile, "run-2", Snapshot(150, 0));
            Assert.AreEqual(9 + 10, reward);
            Assert.AreEqual(150, profile.bestDistance);
            Assert.AreEqual(9, ProfileStore.ApplyRunReward(profile, "run-3", Snapshot(150, 0)));
        }

        [Test]
        public void LegacyWeakEffectsFlagMigratesToWeakLevel()
        {
            var profile = new PlayerProfile { weakEffects = true };
            ProfileStore.Save(profile);
            PlayerProfile loaded = ProfileStore.Load();
            Assert.AreEqual(EffectLevel.Weak, loaded.Effects);
            Assert.AreEqual(EffectLevel.Strong, new PlayerProfile().Effects);
        }

        [Test]
        public void PurchaseUpdatesBalanceAndOwnershipTogether()
        {
            var profile = new PlayerProfile { coinBalance = 200 };
            var sword = new CatalogItem { id = "weapon_wooden_sword", price = 150 };
            Assert.AreEqual(PurchaseResult.Bought, ProfileStore.TryPurchase(profile, sword));
            Assert.AreEqual(50, profile.coinBalance);
            Assert.IsTrue(profile.Owns(sword.id));
            Assert.AreEqual(PurchaseResult.AlreadyOwned, ProfileStore.TryPurchase(profile, sword));
            Assert.AreEqual(50, profile.coinBalance);
            PlayerProfile loaded = ProfileStore.Load();
            Assert.AreEqual(50, loaded.coinBalance);
            Assert.IsTrue(loaded.Owns(sword.id));
        }

        [Test]
        public void PurchaseWithoutEnoughCoinsChangesNothing()
        {
            var profile = new PlayerProfile { coinBalance = 10 };
            Assert.AreEqual(PurchaseResult.NotEnoughCoins, ProfileStore.TryPurchase(profile, new CatalogItem { id = "weapon_banana", price = 200 }));
            Assert.AreEqual(10, profile.coinBalance);
            Assert.IsFalse(profile.Owns("weapon_banana"));
        }

        [Test]
        public void PostMvpItemsAreNotForSale()
        {
            var profile = new PlayerProfile { coinBalance = 999 };
            Assert.AreEqual(PurchaseResult.NotForSale, ProfileStore.TryPurchase(profile, new CatalogItem { id = "character_cat", price = 300, status = "post_mvp" }));
            Assert.AreEqual(999, profile.coinBalance);
        }

        [Test]
        public void CorruptMainFileFallsBackToBackup()
        {
            var profile = new PlayerProfile { coinBalance = 5 };
            ProfileStore.Save(profile);
            profile.coinBalance = 7;
            ProfileStore.Save(profile); // previous file becomes the backup
            File.WriteAllText(Path.Combine(dir, "rhythm_slash_profile.json"), "not json at all");
            Assert.AreEqual(5, ProfileStore.Load().coinBalance);
        }
    }
}
