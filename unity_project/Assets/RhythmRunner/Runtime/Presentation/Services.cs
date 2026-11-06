using System;
using UnityEngine;

namespace RhythmSlashRunner.Presentation
{
    /// <summary>
    /// Round 32: real-money purchases (energy drink packs). The game talks to this interface only. <see cref="Granted"/>
    /// is called for every paid order — including one finished after a crash or on the next launch — and must give the
    /// drinks and save; the <c>done</c> callback of <see cref="Buy"/> only drives the dialog.
    /// </summary>
    public interface IPurchases
    {
        bool Available { get; }
        Action<string> Granted { get; set; }
        void Connect();
        string PriceOf(string productId);
        void Buy(string productId, Action<bool, string> done);
    }

    public static class Products
    {
        public const string Drink3 = "rsr_drink_3";
        public const string Drink10 = "rsr_drink_10";
        public static readonly string[] All = { Drink3, Drink10 };
        public static int DrinksIn(string id) { return id == Drink3 ? 3 : id == Drink10 ? 10 : 0; }
        /// <summary>Shown until the store answers with the real (localised) price.</summary>
        public static string FallbackPrice(string id) { return id == Drink3 ? "¥160" : id == Drink10 ? "¥480" : "--"; }
    }

    /// <summary>
    /// Round 33: Google Play Billing / App Store through Unity IAP 5 (com.unity.purchasing). In the editor Unity IAP runs
    /// its fake store. The products must exist in Google Play Console / App Store Connect with the ids in <see cref="Products"/>
    /// (consumables); until then the store answers with no products and the packs say "準備中".
    /// </summary>
    public sealed class StorePurchases : IPurchases
    {
        UnityEngine.Purchasing.StoreController store;
        bool connected;
        readonly System.Collections.Generic.Dictionary<string, Action<bool, string>> waiting = new System.Collections.Generic.Dictionary<string, Action<bool, string>>();

        public Action<string> Granted { get; set; }

        public bool Available { get { return connected && store != null && store.GetProductById(Products.Drink3) != null; } }

        public async void Connect()
        {
            try
            {
                store = UnityEngine.Purchasing.UnityIAPServices.StoreController();
                store.OnStoreConnected += () =>
                {
                    connected = true;
                    var defs = new System.Collections.Generic.List<UnityEngine.Purchasing.ProductDefinition>();
                    foreach (string id in Products.All) defs.Add(new UnityEngine.Purchasing.ProductDefinition(id, UnityEngine.Purchasing.ProductType.Consumable));
                    store.FetchProducts(defs);
                };
                store.OnStoreDisconnected += d => { connected = false; Debug.LogWarning("[Purchases] store disconnected: " + d.message); };
                store.OnProductsFetched += list => Debug.Log("[Purchases] products: " + list.Count);
                store.OnProductsFetchFailed += f => Debug.LogWarning("[Purchases] products failed: " + f.FailureReason);
                store.OnPurchasePending += order =>
                {
                    string id = FirstId(order);
                    // give first, then confirm: an unconfirmed order comes back on the next launch instead of being lost
                    if (id != null && Granted != null) Granted(id);
                    store.ConfirmPurchase(order);
                    Finish(id, true, null);
                };
                store.OnPurchaseFailed += order =>
                {
                    bool cancelled = order.FailureReason == UnityEngine.Purchasing.PurchaseFailureReason.UserCancelled;
                    Finish(FirstId(order), false, cancelled ? "キャンセルしました" : "購入できませんでした（" + order.FailureReason + "）");
                };
                await store.Connect();
            }
            catch (Exception e) { Debug.LogWarning("[Purchases] store unavailable: " + e.Message); connected = false; }
        }

        static string FirstId(UnityEngine.Purchasing.Order order)
        {
            foreach (UnityEngine.Purchasing.CartItem item in order.CartOrdered.Items()) return item.Product != null ? item.Product.definition.id : null;
            return null;
        }

        void Finish(string id, bool ok, string error)
        {
            Action<bool, string> done;
            if (id == null || !waiting.TryGetValue(id, out done)) return;
            waiting.Remove(id);
            done(ok, error);
        }

        public string PriceOf(string productId)
        {
            UnityEngine.Purchasing.Product p = store != null ? store.GetProductById(productId) : null;
            return p != null && p.metadata != null && !string.IsNullOrEmpty(p.metadata.localizedPriceString) ? p.metadata.localizedPriceString : Products.FallbackPrice(productId);
        }

        public void Buy(string productId, Action<bool, string> done)
        {
            UnityEngine.Purchasing.Product p = Available ? store.GetProductById(productId) : null;
            if (p == null) { done(false, "ストアにつながりません。しばらくしてからお試しください。"); return; }
            waiting[productId] = done;
            store.PurchaseProduct(p);
        }
    }

    /// <summary>A platform leaderboard (Game Center, Google Play Games).</summary>
    public interface IRankingBackend
    {
        bool Ready { get; }
        void SignIn(Action<bool> done);
        void Submit(int distance);
        void Show();
    }

    /// <summary>
    /// Round 32-33 (owner: rankings separate per platform): the weekly distance board. iPhone: Game Center (a recurring weekly
    /// leaderboard in App Store Connect with id <see cref="LeaderboardId"/>). Android: Google Play Games — its backend lives
    /// in the Android-only assembly (Assets/RhythmRunner/Android) and registers itself in <see cref="Backend"/>. Without a
    /// board the game still keeps this week's best on the device.
    /// </summary>
    public static class Ranking
    {
        public const string LeaderboardId = "rsr_weekly_distance";
        public static IRankingBackend Backend;
        static bool signedIn;

        public static bool Supported
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return true;
#else
                return Backend != null && Backend.Ready;
#endif
            }
        }

        public static void SignIn()
        {
#if UNITY_IOS && !UNITY_EDITOR
            Social.localUser.Authenticate(ok => signedIn = ok);
#else
            if (Backend != null && Backend.Ready) Backend.SignIn(ok => signedIn = ok);
#endif
        }

        public static void Submit(int distance)
        {
            if (!signedIn || distance <= 0) return;
#if UNITY_IOS && !UNITY_EDITOR
            Social.ReportScore(distance, LeaderboardId, ok => { if (!ok) Debug.LogWarning("[Ranking] score not sent"); });
#else
            if (Backend != null) Backend.Submit(distance);
#endif
        }

        /// <summary>Opens the platform board; false when there is none (the caller explains).</summary>
        public static bool Show()
        {
            if (!Supported) return false;
            if (!signedIn) { SignIn(); return false; }
#if UNITY_IOS && !UNITY_EDITOR
            Social.ShowLeaderboardUI();
#else
            Backend.Show();
#endif
            return true;
        }
    }
}
