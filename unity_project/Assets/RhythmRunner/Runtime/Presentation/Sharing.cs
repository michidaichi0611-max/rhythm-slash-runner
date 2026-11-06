using System;
using System.IO;
using UnityEngine;

namespace RhythmSlashRunner.Presentation
{
    /// <summary>
    /// Round 34: the result card goes out through the phone's own share sheet (the player picks X, LINE, TikTok…; the game
    /// never posts by itself). Android: the picture is put in the Pictures collection and handed to ACTION_SEND. iPhone:
    /// UIActivityViewController (Assets/Plugins/iOS/RsrShare.mm). PC: saved, and the post text copied to the clipboard.
    /// Returns false when the share sheet could not be opened (the caller explains).
    /// </summary>
    public static class Sharing
    {
        /// <summary>Hashtag on every post (also used for the store page).</summary>
        public const string Hashtag = "#リズムスラッシュランナー";

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void RsrShareImage(string path, string text);
#endif

        public static bool Share(byte[] png, string fileName, string text)
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var resolver = activity.Call<AndroidJavaObject>("getContentResolver"))
                using (var values = new AndroidJavaObject("android.content.ContentValues"))
                using (var media = new AndroidJavaClass("android.provider.MediaStore$Images$Media"))
                using (var collection = media.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI"))
                {
                    values.Call("put", "_display_name", fileName);
                    values.Call("put", "mime_type", "image/png");
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                        if (version.GetStatic<int>("SDK_INT") >= 29) values.Call("put", "relative_path", "Pictures/RhythmSlashRunner");
                    using (var uri = resolver.Call<AndroidJavaObject>("insert", collection, values))
                    {
                        if (uri == null) return false;
                        using (var stream = resolver.Call<AndroidJavaObject>("openOutputStream", uri))
                        {
                            stream.Call("write", (sbyte[])(Array)png);
                            stream.Call("close");
                        }
                        using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.SEND"))
                        {
                            intent.Call<AndroidJavaObject>("setType", "image/png");
                            intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.STREAM", uri);
                            intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", text);
                            intent.Call<AndroidJavaObject>("addFlags", 1); // FLAG_GRANT_READ_URI_PERMISSION
                            using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                            using (var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "シェア"))
                                activity.Call("startActivity", chooser);
                        }
                    }
                }
                return true;
#elif UNITY_IOS && !UNITY_EDITOR
                string path = Path.Combine(Application.temporaryCachePath, fileName);
                File.WriteAllBytes(path, png);
                RsrShareImage(path, text);
                return true;
#else
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "RhythmSlashRunner");
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, fileName), png);
                GUIUtility.systemCopyBuffer = text; // PC: paste it with the picture wherever you post
                return true;
#endif
            }
            catch (Exception e) { Debug.LogWarning("[Share] " + e.Message); return false; }
        }
    }
}
