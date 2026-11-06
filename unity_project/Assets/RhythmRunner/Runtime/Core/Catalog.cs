using System;

namespace RhythmSlashRunner.Core
{
    // JsonUtility mirror of data/content_catalog.json. Cosmetics only: nothing here may change judgement or score.
    [Serializable] public sealed class ContentCatalog { public int schemaVersion; public CatalogItem[] characters; public CatalogItem[] weapons; public ObjectItem[] objects; public CatalogItem[] stages; }

    [Serializable]
    public sealed class CatalogItem
    {
        public string id;
        public string displayName;
        public int price;
        public string status;
        public string trailId;
        public string soundSetId;
        public string musicId;
        // stages (round 2): scenery and the cosmetic object set drawn for the pattern slots
        public string backdropId;
        public string[] objects;
        public string holdObject;
        /// <summary>What the stage draws for a slide (round 11: ramen, toy train…).</summary>
        public string slideObject;
        public string mashObject;
        public string bossId;
        /// <summary>Round 30: the bosses this stage can bring (one per run, picked from the run's seed); empty = <see cref="bossId"/>.</summary>
        public string[] bossIds;

        /// <summary>Post-MVP entries are shown as "in development" and can never be bought.</summary>
        public bool Playable { get { return status != "post_mvp"; } }
    }

    [Serializable] public sealed class ObjectItem { public string id; public string intactPrefabId; public string soundId; public string vfxId; }

    public static class CatalogQueries
    {
        /// <summary>
        /// The object actually drawn for a pattern slot in a stage. Patterns only name material slots (strawberry = soft,
        /// watermelon = juicy, barrel = hard, hold, mash); each stage dresses them differently. Purely cosmetic.
        /// </summary>
        public static string DisplayObject(CatalogItem stage, string slotObjectId)
        {
            if (stage == null || slotObjectId == null) return slotObjectId;
            if (slotObjectId == "object_hold") return stage.holdObject ?? slotObjectId;
            if (slotObjectId == "object_slide") return stage.slideObject ?? "object_ramen";
            if (slotObjectId == "object_mash") return stage.mashObject ?? slotObjectId;
            if (stage.objects == null || stage.objects.Length < 3) return slotObjectId;
            if (slotObjectId == "object_strawberry") return stage.objects[0];
            if (slotObjectId == "object_watermelon") return stage.objects[1];
            if (slotObjectId == "object_barrel") return stage.objects[2];
            return slotObjectId;
        }

        /// <summary>"object_crate" → "crate" (sprite prefix).</summary>
        public static string Kind(string objectId)
        {
            if (string.IsNullOrEmpty(objectId)) return "strawberry";
            return objectId.StartsWith("object_") ? objectId.Substring(7) : objectId;
        }

        public static CatalogItem Find(CatalogItem[] items, string id)
        {
            if (items == null) return null;
            foreach (CatalogItem item in items) if (item.id == id) return item;
            return null;
        }
    }
}
