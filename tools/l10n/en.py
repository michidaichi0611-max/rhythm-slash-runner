"""Round 34: the English text of the game. Keys are the Japanese strings exactly as the code writes them (whole sentences
and the fragments that concatenations are built from); the game swaps them at display time (L10n.cs).
Run:  python tools/l10n/en.py   → unity_project/Assets/RhythmRunner/Resources/l10n_en.json
"""
import json
from pathlib import Path

EN = {
    # daily goals
    "裏拍を20回PERFECT": "20 PERFECT offbeats", "1回で300m走る": "Run 300 m in one go", "ボスを1体たおす": "Defeat 1 boss",
    "PERFECTを150回": "150 PERFECTs", "最大コンボ60": "Max combo 60", "長い物を8本斬りきる": "Finish 8 long objects",
    "回復カプセルを2個取る": "Grab 2 heart capsules", "1回で8万点": "80,000 points in one run", "・": " / ", "達成！": "Done!",
    " コイン": " coins", "スコア倍率 MAX ×1.5": "Score multiplier MAX ×1.5", "コイン ×2": "Coins ×2", "FEVERボーナス +": "FEVER bonus +",
    "枚": "", "次は ": "Next: ",
    # round 36: how to get drinks (shown while the paid packs are hidden)
    "プライバシーポリシー": "Privacy policy", "ドリンクの手に入れ方": "How to get drinks", "・毎日のログインボーナスで 1本": "• 1 from the daily login bonus",
    "・その日 はじめてボスを倒すと 1本": "• 1 for your first boss win of the day",
    "・ボスにやられたら そのプレイは回数を使わない": "• A run lost to a boss doesn't use up a play",
    "・毎日のログインボーナスで 1本\\n・その日 はじめてボスを倒すと 1本\\n・ボスにやられたら そのプレイは回数を使わない\\n・コイン ":
        "• 1 from the daily login bonus\\n• 1 for your first boss win of the day\\n• A run lost to a boss doesn't use up a play\\n• Trade ",
    "枚で 1本と交換": " coins for 1",
    # bosses
    "トースター魔人": "Toaster Fiend", "たまねぎ大王": "Onion King", "ソフトクリーム将軍": "General Soft-Serve", "ドーナツ伯爵": "Count Donut",
    "ゼンマイ大王": "Wind-up King", "カミナリ雲大将": "Thundercloud General", "雪だるま将軍": "Snowman General",
    " があらわれた！": " appears!", "BOSS撃破！": "BOSS DEFEATED!", "♥ +1   コイン +": "♥ +1   Coins +", "怒りモード！": "RAGE MODE!",
    "ためこうげき！": "CHARGE ATTACK!", "ハート回復！": "Heart restored!", "ハート満タン！": "Hearts full!", "コイン +5": "Coins +5",
    "斬撃ラッシュ!!": "SLASH RUSH!!", "前方をぜんぶ斬る！ +20m": "Slash everything ahead! +20m", "巨大化!!": "GIANT!!",
    "8拍のあいだ どのレーンでも斬れる": "Any lane slashes for 8 beats", "バリア！": "BARRIER!", "ミス1回を防ぐ": "Blocks one miss",
    "元に戻った！": "Back to normal!", "レーンに注意": "Watch your lanes", "逃げられた…": "It got away…", "次のボスで取り返そう": "Get the next one!",
    # calibration
    "タイミングを自動で合わせる": "Auto-adjust timing", "もう一度": "Again", "もどる": "Back",
    "「カッ」という音に合わせて、\\n画面の下半分をタップしてください。\\n（最初の4回は聞くだけ。画面は見なくてOK）":
        "Tap the lower half of the screen\\nin time with the click.\\n(Just listen to the first 4. No need to watch.)",
    "聞いて… ": "Listen… ", "あと ": "", " 回": " left",
    "うまく測れませんでした。\\n音に合わせて、もう一度タップしてください。": "Couldn't measure that.\\nPlease tap along with the sound again.",
    "タイミング補正を ": "Timing offset set to ", "ms にしました。\\n（設定の「タイミング補正」でいつでも変えられます）": " ms.\\n(Change it anytime under Timing offset in Settings.)",
    # things you cut
    "いちご": "Strawberry", "スイカ": "Watermelon", "たる": "Barrel", "木箱": "Crate", "ダイヤ": "Diamond", "サイコロ": "Dice",
    "目覚まし": "Alarm clock", "アヒル": "Duck", "フランスパン": "Baguette", "ネギ": "Leek", "えんぴつ": "Pencil", "カボチャ": "Pumpkin", "金庫": "Safe",
    # result card
    "称号": "TITLE", "斬った数": "SLICED", "ボス撃破": "BOSSES", "シェア": "Share", "保存": "Save", "閉じる": "Close",
    "ボスハンター": "Boss Hunter", "伝説のランナー": "Legendary Runner", "コンボの鬼": "Combo Demon", "フィーバー職人": "Fever Artisan",
    "世界を駆ける者": "World Runner", "ボスキラー": "Boss Killer", "パーフェクト名人": "Perfect Master", "マスター": " Master",
    "まな板のサムライ": "Cutting-Board Samurai", "見習いランナー": "Apprentice Runner", "回": "", "、ボス": ", beat ", "体撃破": " boss(es)",
    "リズムスラッシュランナーで ": "I ran ", "m 走った！": " m in Rhythm Slash Runner!", "\\n称号「": "\\nTitle: \"",
    "シェアできませんでした": "Couldn't share", "「保存」で画像を保存してから、好きなアプリで投稿してね。": "Save the picture, then post it from any app.",
    "シェアの準備ができました": "Ready to share", "画像を「ピクチャ」の RhythmSlashRunner に保存し、投稿文をコピーしました。":
        "Saved the picture to Pictures/RhythmSlashRunner and copied the post text.",
    "保存しました": "Saved", "保存できませんでした": "Couldn't save", "端末の空き容量や権限を確認してください。": "Check your storage space and permissions.",
    "写真アプリの「RhythmSlashRunner」アルバム": "the \"RhythmSlashRunner\" album in your photos",
    # app / run
    "アプリを終了しますか？": "Quit the app?", "終了する": "Quit", "キャンセル": "Cancel", "空振り": "Empty swings", "ためこうげき": "Charge attacks",
    "兵隊のこうげき": "Soldier attacks", "ボスの弾": "Boss shots", "長い物": "Long objects", "連打": "Mash", "裏拍": "Offbeats", "ふつうの物": "Regular objects",
    "ハートを失った原因：": "Hearts lost to: ", "エナジードリンク +1": "Energy drink +1", "今日はじめての ボス撃破ボーナス": "First boss of the day bonus",
    # tips
    "ラインに来たら タップ！": "Tap at the line!",
    "物が水色の線に重なった瞬間に、\\nそのレーン（左・中央・右）を押して斬ろう。": "When an object touches the blue line,\\ntap its lane (left, center, right) to slash it.",
    "長い物は 押しっぱなし！": "Hold long objects!", "先頭がラインに来たら押して、\\n最後まで離さずに 斬りきろう。": "Press when the tip reaches the line\\nand keep holding to the end.",
    "くねった物は なぞれ！": "Trace the winding ones!", "押したまま、物に沿って\\n指を左右にすべらせよう。曲がり角は 紫の点。": "Keep pressing and slide your finger\\nalong it. Purple dots mark the turns.",
    "ひびの入ったメロンは 割れる！": "Cracked melons split!", "斬ると2つに割れて、\\n半分ずつ となりのレーンに 転がってくる。": "Slash it and it splits in two —\\neach half rolls into another lane.",
    "爆弾は 斬るな！": "Don't slash bombs!", "導火線がチリチリ言ったら 見送ろう。\\n斬るとコンボが切れる。よけたら ボーナス！": "Hear the fuse hiss? Let it pass.\\nSlashing breaks your combo. Dodging earns a bonus!",
    "かたい物は 連打！": "Mash hard objects!", "どのレーンでもOK。\\nリングが一周するまで たくさん叩いて割ろう。": "Any lane works.\\nTap fast until the ring fills to break it.",
    "紫の点線は 裏拍": "Purple dotted lines are offbeats", "白い線と白い線の まんなか。\\n音の「ウラ」で斬ろう。": "Halfway between the white lines.\\nSlash on the off-beat.",
    "光るカプセルは アイテム": "Glowing capsules are items", "斬ると効果が出るよ。\\n取り逃してもミスにはならない。": "Slash one to use it.\\nMissing one is not a miss.",
    "ボスの弾を 斬って返せ！": "Slash the boss's shots back!", "斬った弾が ボスにはね返ってダメージ。\\n最後は連打で とどめ！": "Slashed shots fly back and hurt the boss.\\nFinish it off by mashing!",
    "兵隊のこうげきを 弾け！": "Parry the soldier!", "剣が光ったら そのレーンをタップ。\\nキン・キン・キン…最後の一撃で ふっとばそう！": "When the sword flashes, tap that lane.\\nClang, clang… the last parry sends it flying!",
    "カラスのコックは 気まぐれ！": "The Crow Cook is unpredictable!", "レーンを跳ぶ・ためこうげき…何が来るかは毎回ちがう。\\n紫の帯は 押しっぱなしで 炎をガード！": "Lane hops, charge attacks… it changes every time.\\nHold on the purple band to block the flame!",
    "金色の弾は チャンス！": "Gold shots are your chance!", "PERFECTで返すと\\nクリティカルで 大ダメージ！": "Return one with a PERFECT\\nfor a critical hit!",
    "はじめての物": "NEW!", "タップして続ける": "Tap to continue", "れんしゅうタイム": "Practice time", "この間は ミスしても ハートは減らない": "Misses cost no hearts for now",
    "データの読み込みに失敗しました。\\nアプリを再インストールしてください。": "Failed to load the game data.\\nPlease reinstall the app.",
    # home / menus
    "プレイ": "PLAY", "ショップ": "Shop", "ランキング": "Ranking", "イベント": "Events", "設定": "Settings", "今日の目標": "Today's goals",
    "\\n\\nひとつ達成するごとに コイン +": "\\n\\nEach goal pays coins +", "\\n（毎日かわります）": "\\n(New goals every day)", "今日の目標 ": "Goals ",
    "ハムスター": "Hamster", "ゴールデンハムスター": "Golden Hamster", "黒猫": "Black Cat", "クロネコ": "Black Cat", "ロボット": "Robot", "ロボ": "Robo",
    "ペンギン": "Penguin", "柴犬": "Shiba", "シバイヌ": "Shiba Inu", "おとっつぁん": "Pops", "透明人間": "Invisible Kid", "トウメイニンゲン": "Invisible Kid",
    "鳥じい": "Grandpa Hawk", "トリジイ": "Grandpa Hawk", "白ハム": "Snow Hamster", "シロハム": "Snow Hamster", "ゼリーザメ": "Jelly Shark",
    "使用中": "In use", "タップで使う": "Tap to use", "キャラクターの違いは見た目だけ。判定とスコアは全員共通です。": "Characters only change looks. Timing and score are the same for all.",
    "コインが足りません": "Not enough coins", "まで あと ": ": ", " コイン。\\n走ってコインを集めよう！": " more coins.\\nRun to collect coins!",
    "を仲間にする": ": recruit", " コインで仲間にしますか？\\n（見た目だけ。判定とスコアは同じです）": " coins to recruit?\\n(Looks only. Timing and score stay the same.)",
    "仲間にする": "Recruit", "購入できませんでした": "Purchase failed", "もう一度お試しください。": "Please try again.",
    "が仲間になった！": " joined you!", "ホームからそのまま走れます。": "Run with them from the home screen.",
    # settings
    "音楽": "Music", "効果音": "Sound effects", "合図音": "Cue sounds", "タイミング補正": "Timing offset",
    "判定が音より遅く感じたら＋へ": "If hits feel late, go +", "自動で合わせる": "Auto-adjust", "演出の強さ": "Effects", "弱": "Low", "標準": "Normal", "強": "High",
    "画面の揺れ": "Screen shake", "振動": "Vibration", "背景の動きをおさえる（酔いやすい方に）": "Calmer background (motion sickness)",
    "プレイ記録を送る（匿名・改善のため）": "Send play data (anonymous)", "診断表示（開発用）": "Diagnostics (dev)",
    "チュートリアルとヒントをもう一度": "Show the tutorial and tips again", "次のプレイで表示します": "Shown next time you play",
    "最初の練習と、はじめて出てくる物のヒントをもう一度表示します。": "The first practice and the tips for new things will show again.",
    "ライセンス": "Licenses", "初期値に戻す": "Reset", "設定を初期値に戻す": "Reset settings",
    "音量・補正・演出の設定を初期値に戻します。\\nコインと所持品は消えません。": "Volume, timing and effect settings go back to default.\\nCoins and items are kept.",
    "戻す": "Reset",
    "フォント: M PLUS Rounded 1c (Bold / ExtraBold)\\nCopyright 2016 The Rounded M+ Project Authors.\\nSIL Open Font License 1.1\\n\\nゲーム内の画像はAI画像生成で制作しています。\\n効果音とBGMはアプリ内で合成しています。\\n\\n":
        "Font: M PLUS Rounded 1c (Bold / ExtraBold)\\nCopyright 2016 The Rounded M+ Project Authors.\\nSIL Open Font License 1.1\\n\\nThe game's pictures were made with AI image generation.\\nSound effects and music are synthesized in the app.\\n\\n",
    "一時停止": "Paused", "再開": "Resume", "やり直す": "Restart", "最初からやり直しますか？\\n今回の記録とコインは保存されません。": "Restart from the beginning?\\nThis run's record and coins won't be saved.",
    "ホームへ": "Home", "ホームへ戻る": "Back to home", "ホームに戻りますか？\\n今回の記録とコインは保存されません。": "Go back to home?\\nThis run's record and coins won't be saved.",
    "プレイの前に": "Before you play",
    "このゲームには光の演出や画面の動きがあります。\\nまれに光の刺激で体調を崩す方がいます。\\n明るい部屋で、画面から離れてプレイし、\\n気分が悪くなったらすぐに休んでください。\\n\\n演出の強さはあとから設定で変えられます。":
        "This game has flashing lights and screen motion.\\nA few people may feel unwell from light effects.\\nPlay in a bright room at a distance from the screen,\\nand rest right away if you feel sick.\\n\\nYou can change the effect strength in Settings.",
    # result
    "距離": "Distance", "スコア": "Score", "最大コンボ": "Max combo", "コイン": "Coins", "保存に失敗しました": "Save failed", "ホーム": "Home", "結果カード": "Result card",
    "ボス戦でのゲームオーバー：プレイ回数を1回 返しました": "Game over in a boss fight: 1 play refunded",
    "今日の目標「": "Goal \"", "」達成！ +": "\" done! +", "\\n今日の目標を ぜんぶ達成！ エナジードリンク +1": "\\nAll of today's goals done! Energy drink +1",
    "保存を再試行": "Retry save", "斬った数 +": "Sliced +", "（うちFEVER +": " (FEVER +", "）": ")", "   距離 +": "   Distance +", "   ベスト更新 +": "   New best +",
    "ベストまで あと ": "Best is ", " m！": " m away!", "楽しんでもらえてる？": "Enjoying the game?",
    "よかったら ★で応援してね！\\n開発のはげみになります。": "A ★ rating would mean a lot!\\nIt keeps us going.", "評価する": "Rate", "あとで": "Later",
    "が買えます！": ": you can buy it!", "所持": "Owned", "装備中": "Equipped", "装備する": "Equip", " 入手": " Get", "（試着中）": " (trying on)",
    "武器は見た目と効果音だけが変わります（判定は共通）": "Weapons change looks and sounds only (timing is the same)",
    " 枚。\\nランでコインを集めよう！": " more coins.\\nRun to collect coins!", "プレイする": "Play", "とじる": "Close", "を入手": ": get",
    " コイン\\n所持 ": " coins\\nWallet ", "入手する": "Get", "やめる": "Cancel", "入手できませんでした": "Couldn't get it",
    "保存に失敗しました。もう一度お試しください。": "Save failed. Please try again.", "コインが足りません。": "Not enough coins.", "すぐ装備": "Equip now", " を開封！": ": opening!",
    # plays, drinks, shop
    "あと": "", "日 遊びほうだい！": " days of free play!", "プレイ ": "Plays ", "  あと": "  ", "で+1": " to +1", "  満タン": "  Full",
    "\\nエナジードリンク ×": "\\nEnergy drinks ×", "プレイ回数": "Plays", "ショップへ": "To the shop", "エナジードリンクを飲む？": "Drink an energy drink?",
    "\\n\\n1本でプレイ回数が 満タンに！": "\\n\\nOne drink refills your plays!", "飲む": "Drink", "プレイ回数がありません": "No plays left",
    "\\n\\nエナジードリンクで すぐ満タンに。": "\\n\\nAn energy drink refills them at once.", "ゴクッ！": "Gulp!", "プレイ回数が 満タンになった！\\n": "Plays refilled!\\n",
    "ゲームの改善にご協力ください": "Help us improve the game",
    "どこで失敗しやすいか などの遊び方の記録を、名前などの個人情報なしで送ってもいいですか？\\nあとから設定で いつでも止められます。":
        "May we send play data (like where runs end), with no name or personal info?\\nYou can turn it off anytime in Settings.",
    "送ってOK": "Allow", "送らない": "Don't allow", "ログインボーナス": "Login bonus", "エナジードリンク +1\\n（所持 ": "Energy drink +1\\n(You have ",
    "本）\\n\\nまた明日も来てね！": ")\\n\\nSee you tomorrow!", "キャラ": "Characters", "武器": "Weapons", "アイテム": "Items", "特別": "Special",
    "飲んで満タン": "Drink & refill", "エナジードリンク 1本": "Energy drink ×1", "エナジードリンク 3本パック": "Energy drink 3-pack",
    "エナジードリンク 10本パック": "Energy drink 10-pack", "ドリンク1本で プレイ回数が満タン（": "One drink refills your plays (", "回）。プレイ回数は ": "). One play comes back every ",
    "分で1回 回復します。": " minutes.", "準備中": "Coming soon",
    "お金での購入は 準備中です（ストアにつながっていません）。\\nコインでの購入や ログインボーナスを使ってね。": "Purchases aren't available yet (no store connection).\\nUse coins or the login bonus for now.",
    "エナジードリンク ": "Energy drink ×", "本": "", " で購入します。": " — buy?", "購入する": "Buy", "ありがとう！": "Thank you!", "エナジードリンク +": "Energy drinks +",
    "\\n（所持 ": "\\n(You have ", "本）": ")", "限定アイテム": "Limited items",
    "イベントの景品や 限定の武器・キャラが\\nここに並びます。（準備中）": "Event rewards and limited weapons and characters\\nwill appear here. (Coming soon)",
    "週間ランキング": "Weekly ranking", "今週のベスト": "This week's best", "これまでのベスト ": "All-time best ", "月曜日にリセット（あと ": "Resets on Monday (", " 日）": " days left)",
    "ランキングを見る": "View ranking", "Game Center に サインインしてから もう一度押してね。": "Sign in to Game Center, then tap again.",
    "Android版のランキングは 準備中です（Google Play ゲームズとの連携が必要）。": "The Android ranking is coming soon (needs Google Play Games).",
    "ランキングは スマホ版で見られます。": "The ranking is available on phones.", "iPhone と Android は 別々のランキングです。": "iPhone and Android have separate rankings.",
    "季節のイベント": "Seasonal events",
    "期間中に流れてくる 季節の特別な物を斬って\\nポイントを集めよう！\\n終わったら 限定の武器やキャラと交換できるよ。\\n\\n（ただいま準備中）":
        "Slash the special seasonal objects that appear\\nduring the event to collect points!\\nTrade them for limited weapons and characters.\\n\\n(Coming soon)",
    # in the run
    "♪ 裏拍がくるよ！ 拍と拍のあいだ": "♪ Offbeats! Between the beats", "一閃!!": "FINISH!!", "長押し！": "HOLD!", "はなせ！": "RELEASE!", "そのまま！": "KEEP HOLDING!",
    "← なぞれ！": "← TRACE!", "なぞれ！ →": "TRACE! →", "なぞれ！": "TRACE!", "とどめ！！": "FINISH IT!!", "連打！！": "MASH!!", "連打！": "MASH!",
    "斬撃ラッシュ GET!": "SLASH RUSH GET!", "巨大化 GET!": "GIANT GET!", "バリア GET!": "BARRIER GET!", "ガード！": "GUARD!", "カウンター！！": "COUNTER!!",
    "おしい！": "SO CLOSE!", "粉砕！！": "SMASHED!!", "キン！": "CLANG!", "カン": "CLINK", "反撃！！": "COUNTER!!", "はじき返した！": "PARRIED!",
    "ドカン！": "BOOM!", "ペシャ！": "SQUISH!", "よけた！": "DODGED!", "キャンセルしました": "Cancelled", "購入できませんでした（": "Purchase failed (",
    "ストアにつながりません。しばらくしてからお試しください。": "Can't reach the store. Please try again later.", "#リズムスラッシュランナー": "#RhythmSlashRunner",
    # catalog (names)
    "ランナー": "Runner", "エネルギーブレード": "Energy Blade", "木刀": "Wooden Sword", "バナナブレード": "Banana Blade", "コロコロ": "Lint Roller",
    "菜箸": "Cooking Chopsticks", "氷の爪": "Ice Claws", "カウボーイの縄": "Cowboy Lasso", "段ボールの剣": "Cardboard Sword", "煙のバット": "Smoke Bat",
    "巨大キッチン": "Giant Kitchen", "おもちゃ部屋": "Toy Room", "空中果樹園": "Sky Orchard", "ネオン市場": "Neon Market", "おかしの国": "Candy Land",
    "こおりの冷凍庫": "Icy Freezer",
    # strings added in later rounds (tips, banners)
    "ひびの入ったメロン": "Cracked melon",
    # settings: language (the Japanese choice stays Japanese)
    "言語 Language": "Language", "日本語": "日本語", "言語：日本語": "言語：日本語", "アプリを再起動すると 日本語になります。": "アプリを再起動すると 日本語になります。",
}


def main():
    entries = [{"ja": k.replace("\\n", "\n"), "en": v.replace("\\n", "\n")} for k, v in EN.items()]
    out = Path(__file__).resolve().parents[2] / "unity_project" / "Assets" / "RhythmRunner" / "Resources" / "l10n_en.json"
    out.write_text(json.dumps({"entries": entries}, ensure_ascii=False, indent=1), encoding="utf-8")
    print(len(entries), "entries →", out)


if __name__ == "__main__":
    main()
