using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEngine;
using Newtonsoft.Json;
using Teal;

/*
 * Balance team games based on the ranks on match start and/or on !bal command.
 * author: @noerw
 * script version: 0.1 (beta)
 * target game version: 0.8.60
 */

// TODO: only balance selected players: who joined this match - or if none, those without caps.

[RequireComponent(typeof(Match))]
[RequireComponent(typeof(Teams))]
[DisallowMultipleComponent]
// ReSharper disable once CheckNamespace
public class RankBalance : Modifiable
{
    [JsonProperty("BalanceOnMatchStart")] public bool balanceOnStart = true;
    [JsonProperty("BalanceOnCommand")] public bool balanceOnCommand = true;
    [JsonProperty("BalanceOnPlayerChange")] public bool balanceOnPlayerChange = true;
    [JsonProperty("BalanceBots")] public bool balanceBots = true;
    [JsonProperty("AssignOnJoin")] public bool assignOnJoin = true; // only set true, when builtin AutoBalance is off.
    [JsonProperty("AddFillBots")] public bool addFillBots = true;
    [JsonProperty("AddFillBotMin")] public int addFillBotMin = 6;
    [JsonProperty("MinPlayersForScoreUpdate")] public int minPlayersForScoreUpdate = 4;
    [JsonProperty("MinScoreDeltaForRebalance")] public int minScoreDeltaForRebalance = 3;
    private string basePath = "RankBalance";
    private List<string> playersJoinedMidGame = new List<string>();

    private float autoBalanceTimeoutSecs = 45f;
    private bool balanceScheduled = false;
    private bool balanceScheduledManually = false;
    private int  currentSpectators = 0;
    private float lastBalanceAt = -1f;

    private System.Func<Player, float> scoreFunc;
    private string scoreFuncName;
    Dictionary<string, object> pubscores;

    private void Awake()
    {
        int line = 0;
        try
        {
            line = 1;
            if (!Net.IsServer) return;
            line = 2;
            var teams = GetComponent<Teams>();
            line = 3;
            if (!teams || !teams.IsTeamsGame()) return;
            line = 4;
            
            Eventor.AddListener(Events.Player_Joined, OnPlayerJoined);
            Eventor.AddListener(Events.Player_Left, OnPlayerLeft);
            Eventor.AddListener(Events.Player_Changed_Team, OnPlayerChangedTeam);
            Eventor.AddListener(Events.Match_Ended, OnMatchEnd);
            Eventor.AddListener(Events.Flag_Captured, OnFlagReturned);
            Eventor.AddListener(Events.Flag_Returned, OnFlagReturned);
            GameChat.instance.OnChat.AddListener(OnPlayerChat);

            line = 5;
            // set defaults, likely overriden by LoadConfig()
            scoreFunc = PlayerRank;
            line = 6;
            scoreFuncName = "rank";
            LoadConfig();
            line = 7;

            // set initial state
            currentSpectators = Players.Get.GetSpecators().Count; // only legit with typos in funcnames
            line = 8;

            string filepath = ScorePath();
            line = 9;
            if (FileHelper.Exists(filepath))
                pubscores = FileHelper.ReadJson(filepath);
            else
                pubscores = new Dictionary<string, object>();
            line = 10;

            if (balanceOnStart)
                BalanceTeams();
            line = 11;
        } catch (System.Exception ex) {
            Debug.Log($"[RankBalance.Awake] @@@@@ ERROR @@@@@ AT LINE " + line);
            try {
                Debug.Log($"[RankBalance.Awake] Exception HRESULT " + ex.HResult.ToString("X") + " type " + ex.GetType());
                Debug.Log($"[RankBalance.Awake] Exception message: " + ex.Message);
                Debug.Log($"[RankBalance.Awake] Exception trace: " + ex.StackTrace);
                var baseEx = ex.GetBaseException();
                Debug.Log($"[RankBalance.Awake] BaseEx: " + baseEx);
            } catch (System.Exception ex2) {
                Debug.Log($"[RankBalance.Awake] Another exception occured inside handler, HRESULT " + ex2.HResult.ToString("X") + " type " + ex2.GetType());
            }
        }
    }

    private void OnDestroy() {
        try
        {
            //if (!Net.IsServer) return;
            //if (!GetComponent<Teams>().IsTeamsGame()) return;
            Eventor.RemoveListener(Events.Player_Joined, OnPlayerJoined);
            Eventor.RemoveListener(Events.Player_Left, OnPlayerLeft);
            Eventor.RemoveListener(Events.Player_Changed_Team, OnPlayerChangedTeam);
            Eventor.RemoveListener(Events.Match_Ended, OnMatchEnd);
            Eventor.RemoveListener(Events.Flag_Captured, OnFlagReturned);
            Eventor.RemoveListener(Events.Flag_Returned, OnFlagReturned);
            GameChat.instance.OnChat.RemoveListener(OnPlayerChat);
        } catch (System.Exception ex) {
            Debug.Log($"[RankBalance.OnDestroy] @@@@@ ERROR @@@@@");
            try {
                Debug.Log($"[RankBalance.OnDestroy] Exception HRESULT " + ex.HResult.ToString("X") + " type " + ex.GetType());
                Debug.Log($"[RankBalance.OnDestroy] Exception message: " + ex.Message);
                Debug.Log($"[RankBalance.OnDestroy] Exception trace: " + ex.StackTrace);
            } catch (System.Exception ex2) {
                Debug.Log($"[RankBalance.OnDestroy] Another exception occured inside handler, HRESULT " + ex2.HResult.ToString("X") + " type " + ex2.GetType());
            }
        }
    }

    void OnMatchEnd (IGameEvent ev) {
        try {
            SavePlayerScores();
            playersJoinedMidGame.Clear();
        } catch (System.Exception ex) {
            Debug.Log($"[RankBalance.OnMatchEnd] @@@@@ ERROR @@@@@");
            try {
                Debug.Log($"[RankBalance.OnMatchEnd] Exception HRESULT " + ex.HResult.ToString("X") + " type " + ex.GetType());
                Debug.Log($"[RankBalance.OnMatchEnd] Exception message: " + ex.Message);
                Debug.Log($"[RankBalance.OnMatchEnd] Exception trace: " + ex.StackTrace);
            } catch (System.Exception ex2) {
                Debug.Log($"[RankBalance.OnMatchEnd] Another exception occured inside handler, HRESULT " + ex2.HResult.ToString("X") + " type " + ex2.GetType());
            }
        }
    }

    void OnPlayerJoined(IGameEvent ev) {
		try {
			Player p = ((GlobalPlayerEvent)ev).Player;
            if (!p)
                return;
            if (p.IsBot())
            {
                if (assignOnJoin) {
                    int players0 = GetHumansOfTeamExcept(0, p).Count();
                    int players1 = GetHumansOfTeamExcept(1, p).Count();
                    if (players0 == players1)
                        AssignTeam(p, NextTeam(WinningTeam()));
                    else
                        AssignTeam(p, players1 < players0 ? 1 : 0);
                }
            }
            
			playersJoinedMidGame.Add(PlayerPFID(p));

			var teams = GetComponent<Teams>().teams;
			int scoreDelta = System.Math.Abs(teams[0].score - teams[1].score);

			// we dont want to balance on join. or do we? only when score diff is high? when num players joined / left is high? rankdelta is high?

			ManageFillBots();

			if (balanceOnPlayerChange && scoreDelta >= minScoreDeltaForRebalance) {
				if (BalanceIfAllowed(false))
					return;
			}

			if (assignOnJoin) {
				int players0 = GetHumansOfTeamExcept(0, p).Count();
				int players1 = GetHumansOfTeamExcept(1, p).Count();
				if (players0 == players1)
					AssignTeam(p, NextTeam(WinningTeam()));
				else
					AssignTeam(p, players1 < players0 ? 1 : 0);
			}
		} catch (System.Exception ex) {
			Debug.Log($"[RankBalance.OnPlayerJoined] @@@@@ ERROR @@@@@");
			try {
				Debug.Log($"[RankBalance.OnPlayerJoined] Exception HRESULT " + ex.HResult.ToString("X") + " type " + ex.GetType());
				Debug.Log($"[RankBalance.OnPlayerJoined] Exception message: " + ex.Message);
				Debug.Log($"[RankBalance.OnPlayerJoined] Exception trace: " + ex.StackTrace);
			} catch (System.Exception ex2) {
				Debug.Log($"[RankBalance.OnPlayerJoined] Another exception occured inside handler, HRESULT " + ex2.HResult.ToString("X") + " type " + ex2.GetType());
			}
		}
    }

    void OnPlayerLeft(IGameEvent ev) {
        try {
            Player p = ((GlobalPlayerEvent)ev).Player;
            if (!p || p.IsBot())
                return;
            ManageFillBots();
            if (balanceOnPlayerChange)
                BalanceIfAllowed(false);
        } catch (System.Exception ex) {
            Debug.Log($"[RankBalance.OnPlayerLeft] @@@@@ ERROR @@@@@");
            try {
                Debug.Log($"[RankBalance.OnPlayerLeft] Exception HRESULT " + ex.HResult.ToString("X") + " type " + ex.GetType());
                Debug.Log($"[RankBalance.OnPlayerLeft] Exception message: " + ex.Message);
                Debug.Log($"[RankBalance.OnPlayerLeft] Exception trace: " + ex.StackTrace);
            } catch (System.Exception ex2) {
                Debug.Log($"[RankBalance.OnPlayerLeft] Another exception occured inside handler, HRESULT " + ex2.HResult.ToString("X") + " type " + ex2.GetType());
            }
        }
    }

    void OnPlayerChangedTeam(IGameEvent ev) {
        try {
            // whenever a player goes into / leaves spectator, treat is as if a player left.
            Player p = ((GlobalPlayerEvent)ev).Player;
            if (!p || p.IsBot())
                return;
            int count = Players.Get.GetSpecators().Count();
            if (count != currentSpectators && balanceOnPlayerChange)
                OnPlayerLeft(ev);
            currentSpectators = count;
        } catch (System.Exception ex) {
            Debug.Log($"[RankBalance.OnPlayerChangedTeam] @@@@@ ERROR @@@@@");
            try {
                Debug.Log($"[RankBalance.OnPlayerChangedTeam] Exception HRESULT " + ex.HResult.ToString("X") + " type " + ex.GetType());
                Debug.Log($"[RankBalance.OnPlayerChangedTeam] Exception message: " + ex.Message);
                Debug.Log($"[RankBalance.OnPlayerChangedTeam] Exception trace: " + ex.StackTrace);
            } catch (System.Exception ex2) {
                Debug.Log($"[RankBalance.OnPlayerChangedTeam] Another exception occured inside handler, HRESULT " + ex2.HResult.ToString("X") + " type " + ex2.GetType());
            }
        }
    }

    void OnPlayerChat(Player p, string msg) {
        try {
            if (!p) return;
            IEnumerable<string> args = msg.Split(' ').ToList();
            string cmd = args.First();
            args = args.Skip(1);
            if (cmd.StartsWith("!switch"))
                OnSwitchCmd(p, args);
            else if ((balanceOnCommand || IsAdmin(p)) && cmd.StartsWith("!bal"))
                OnBalanceCmd(p);
            else if (IsAdmin(p) && cmd.StartsWith("!scores"))
                OnScoresCmd(p);
            else if (IsAdmin(p) && cmd.StartsWith("!set")) {
                string arg = args.First();
                if (arg.StartsWith("scorefun") && args.ToList().Count() == 2)
                    SetScoreFunc(args.ToList()[1], true);
                if (arg.StartsWith("fill") && args.ToList().Count() == 2)
                    addFillBotMin = System.Int32.Parse(args.ToList()[1]);
                else if (arg.StartsWith("bot"))
                    addFillBots = !addFillBots;
                else if (arg.Contains("change"))
                    balanceOnPlayerChange = !balanceOnPlayerChange;
                else return;
                SaveConfig();
            }
        } catch (System.Exception ex) {
            Debug.Log($"[RankBalance.OnPlayerChat] @@@@@ ERROR @@@@@");
            try {
                Debug.Log($"[RankBalance.OnPlayerChat] Exception HRESULT " + ex.HResult.ToString("X") + " type " + ex.GetType());
                Debug.Log($"[RankBalance.OnPlayerChat] Exception message: " + ex.Message);
                Debug.Log($"[RankBalance.OnPlayerChat] Exception trace: " + ex.StackTrace);
            } catch (System.Exception ex2) {
                Debug.Log($"[RankBalance.OnPlayerChat] Another exception occured inside handler, HRESULT " + ex2.HResult.ToString("X") + " type " + ex2.GetType());
            }
        }
    }

    void OnScoresCmd (Player caller) {
        // FIXME: this is broken after match has ended..?
        var scores = Players.Get.GetHumans()
            .Where(p => pubscores.ContainsKey(PlayerPFID(p)))
            .Select(p => PubScore.fromDict(p.nick, pubscores[PlayerPFID(p)] as Dictionary<string, object>))
            .ToList();
        scores.Sort((s1, s2) => (int)(100f * (s2.ComputeScore() - s1.ComputeScore())));
        foreach (PubScore s in scores)
            GameChat.instance.ServerChat(s.ToString());
    }

    void OnBalanceCmd (Player caller) {
        BalanceIfAllowed(true);
    }

    void OnSwitchCmd (Player caller, IEnumerable<string> playerNames) {
        if (playerNames.Count() < 1) {
            AssignTeam(caller, NextTeam(caller.GetTeam()));
            return;
        }
        if (!IsAdmin(caller))
            return;
        foreach (string name in playerNames) {
            Player p = FindPlayerByName(name);
            if (p )
                GameChat.instance.ServerChat($"warn: no player matched for '{name}'", caller.id);
            else
                AssignTeam(p, NextTeam(p.GetTeam()));
        }
    }

    void OnFlagReturned(IGameEvent ev) {
        try {
            Flag flag = ev.Sender.GetComponent<Flag>();
            if (balanceScheduled) {
                if (BalanceProhibited(balanceScheduledManually)) {
                    if (balanceScheduledManually)
                        GameChat.instance.ServerChat($"Scheduled balancing canceled due to time constraints.");
                    else
                        GameChat.ChatOrLog($"Scheduled balancing canceled due to time constraints.");
                } else {
                    if (BalanceTeams() > 0)
                        lastBalanceAt = RealTime.timeSinceLevelLoad;
                }
                balanceScheduled = false;
                balanceScheduledManually = false;
            }
        } catch (System.Exception ex) {
            Debug.Log($"[RankBalance.OnFlagReturned] @@@@@ ERROR @@@@@");
            try {
                Debug.Log($"[RankBalance.OnFlagReturned] Exception HRESULT " + ex.HResult.ToString("X") + " type " + ex.GetType());
                Debug.Log($"[RankBalance.OnFlagReturned] Exception message: " + ex.Message);
                Debug.Log($"[RankBalance.OnFlagReturned] Exception trace: " + ex.StackTrace);
            } catch (System.Exception ex2) {
                Debug.Log($"[RankBalance.OnFlagReturned] Another exception occured inside handler, HRESULT " + ex2.HResult.ToString("X") + " type " + ex2.GetType());
            }
        }
    }

    void LoadConfig()
    {
        var line = 0;
        try
        {
            line = 1;
            string filepath = ConfigPath();
            line = 2;
            if (!FileHelper.Exists(filepath)) return;
            line = 3;
            Dictionary<string, object> conf = FileHelper.ReadJson(filepath) as Dictionary<string, object>;
            line = 4;
            if (conf.ContainsKey("scoreFunc"))
            {
                var res = conf["scoreFunc"] as string ?? scoreFuncName;
                SetScoreFunc(res);
            }
            line = 5;
            if (conf.ContainsKey("addFillBots")) addFillBots = (bool?)conf["addFillBots"] ?? addFillBots;
            line = 6;
            if (conf.ContainsKey("addFillBotMin")) Debug.Log("addFillBotMin is " + conf["addFillBotMin"] + " type is " + conf["addFillBotMin"].GetType());
            if (conf.ContainsKey("addFillBotMin")) addFillBotMin = (int?)(long?)conf["addFillBotMin"] ?? addFillBotMin;
            line = 7;
            if (conf.ContainsKey("balanceOnPlayerChange")) balanceOnPlayerChange = (bool?)conf["balanceOnPlayerChange"] ?? balanceOnPlayerChange;
            line = 8;
        } catch (System.Exception ex) {
            Debug.Log($"[RankBalance.LoadConfig] @@@@@ ERROR @@@@@ AT LINE " + line);
            try {
                Debug.Log($"[RankBalance.LoadConfig] Exception HRESULT " + ex.HResult.ToString("X") + " type " + ex.GetType());
                Debug.Log($"[RankBalance.LoadConfig] Exception message: " + ex.Message);
                Debug.Log($"[RankBalance.LoadConfig] Exception trace: " + ex.StackTrace);
            } catch (System.Exception ex2) {
                Debug.Log($"[RankBalance.LoadConfig] Another exception occured inside handler, HRESULT " + ex2.HResult.ToString("X") + " type " + ex2.GetType());
            }
        }
    }

    void SaveConfig() {
        FileHelper.WriteJson(ConfigPath(), new Dictionary<string, object>() {
            {"scoreFunc",scoreFuncName},
            {"addFillBots",addFillBots},
            {"addFillBotMin",addFillBotMin},
            {"balanceOnPlayerChange",balanceOnPlayerChange},
        });
    }

    void SetScoreFunc(string name, bool interactive = false) {
        if (name.ToLower().StartsWith("pub")) {
            if (interactive) GameChat.instance.ServerChat($"Next balance based on pub scores");
            else GameChat.ChatOrLog($"Next balance based on pub scores");
            scoreFunc = PlayerPubscore;
            scoreFuncName = "pub score";
        } else if (name.ToLower().StartsWith("rank")) {
            if (interactive) GameChat.instance.ServerChat($"Next balance based on rankedings");
            else GameChat.ChatOrLog($"Next balance based on rankedings scores");
            scoreFunc = PlayerRank;
            scoreFuncName = "rank";
        }
    }

    void ManageFillBots() {
        if (!addFillBots) return;
        int pcount = Players.Get.GetHumansNonSpectator().Count();
        int botcount = addFillBotMin;
        if (pcount > addFillBotMin) {
            if (pcount % 2 == 0) botcount = pcount;
            else botcount = pcount + 1;
        }
        Global.FillBotsCount = botcount;
    }

    bool ShouldScheduleBalance() {
        // balance later, when the situation is resolved:
        return Flag._flags.Exists(f => !f.inbase);
    }

    bool BalanceProhibited(bool isManualBalance) {
        // dont balance now, if..
        return (
            // the match is about to end
            (float)GetComponent<Match>().matchSecs - RealTime.timeSinceLevelLoad < 60f ||
            // an automatic balance is called after we just balanced
            (!isManualBalance && lastBalanceAt >= 0f && RealTime.timeSinceLevelLoad - lastBalanceAt < autoBalanceTimeoutSecs)
        );
    }

    bool BalanceIfAllowed (bool interactive = false) {
        if (BalanceProhibited(interactive)) {
            if (interactive)
                GameChat.instance.ServerChat($"Not balancing now - too frequent or late in the match.");
            else
                GameChat.ChatOrLog($"Not balancing now - too frequent or late in the match.");
            return false;
        }

        if (ShouldScheduleBalance()) {
            if (interactive) GameChat.instance.ServerChat($"Rebalancing scheduled.");
            else GameChat.ChatOrLog($"Rebalancing scheduled.");
            balanceScheduled = true;
            balanceScheduledManually = interactive;
            return false;
        }

        if (BalanceTeams() > 0)
            lastBalanceAt = RealTime.timeSinceLevelLoad;
        return true;
    }

    int BalanceTeams(bool dryrun=false, int teamPrecedence=0) {
        // This function prioritizes team size over optimal rank distribution.
        // NOTE: balancing by rank is a https://en.wikipedia.org/wiki/Subset_sum_problem

        if (!Net.IsServer) return 0;
        if (Teams.instance.GetTeamsCount() != 2) return 0;

        List<Player> players = Players.Get.GetPlayersNonSpecators();
        if (!balanceBots)
            players = Players.Get.GetHumansNonSpectator();

        // if (players.Count < 3) // balancing doesn't make sense with < 3 players
        //     return 0;

        // find out which initial team assignment causes the least switches by doing dry runs first
        if (!dryrun)
            teamPrecedence = (BalanceTeams(true, 0) > BalanceTeams(true, 1)) ? 1 : 0;

        // do the balancing by putting best & worst unassigned players pairwise
        // into a team. this results very often in the optimal solution, but not always.
        // even when not, delta-to-optimal is in an acceptable 15% range.
        int smallerTeamSize = players.Count / 2; // NOTE: deliberate integer division
        int targetTeam = teamPrecedence % 2;
        float[] sumOfScores = new float[2];
        int[] teamCount  = new int[2];
        int playersSwitched = 0;

        // here we determine ranking. scale by 100 to work around the int interface of Sort()
        players.Sort((p1, p2) => (int)(100f * (scoreFunc(p2) - scoreFunc(p1))));

        for(int i = 0; i < smallerTeamSize; i++) {
            // NOTE: this loop assigns two players per iteration.

            // if team size is not divisible by 2, don't put the last pair in the same team, but put each in a different one.
            // prefer putting the better player in team 1, as team 0 already got the best player.
            bool isLastPairOddTeamsize = (i == smallerTeamSize-1 && smallerTeamSize%2 == 1);
            if (isLastPairOddTeamsize)
                targetTeam = NextTeam(targetTeam);

            if (AssignTeam(players[i], targetTeam, dryrun))
                playersSwitched++;
            sumOfScores[targetTeam] += scoreFunc(players[i]);
            teamCount[targetTeam] += 1;

            if (smallerTeamSize > 0) {
                if (isLastPairOddTeamsize)
                    targetTeam = NextTeam(targetTeam);

                Player p2 = players[2*smallerTeamSize-1-i];
                if (AssignTeam(p2, targetTeam, dryrun))
                    playersSwitched++;
                sumOfScores[targetTeam] += scoreFunc(p2);
                teamCount[targetTeam] += 1;
            }
            targetTeam = NextTeam(targetTeam);
        }

        // if there is an uneven amount of players, put the remaining lowest ranked into the weaker team
        bool teamsEvenlySized = players.Count % 2 == 0;
        if (!teamsEvenlySized) {
            Player lowest = players[players.Count-1];
            // if team 0 is stronger, or teams are equal (team 0 got highest ranking
            // player, so put them in team 1)
            if (AssignTeam(lowest, sumOfScores[0] - sumOfScores[1] >= 0f ? 1 : 0, dryrun))
                playersSwitched++;
            sumOfScores[targetTeam] += scoreFunc(lowest);
            teamCount[targetTeam] += 1;
        }

        if (dryrun) return playersSwitched;

        if (playersSwitched > 0) {
            GameChat.instance.ServerChat($"Teams ({teamCount[0]}v{teamCount[1]}) balanced based on {scoreFuncName}");
        } else {
            GameChat.instance.ServerChat($"Teams ({teamCount[0]}v{teamCount[1]}) balanced - but unchanged");
        }

        return playersSwitched;
    }

    // FIXME: factor the PubScore related stuff out of this, so we can actually have them for DM too.
    void SavePlayerScores () {
        var matchStats = MonoSingleton<Stats>.Get.stats;
        bool isTie = TeamsTied();
        int winningTeam = WinningTeam();

        var players = Players.Get.GetHumansNonSpectator().Where(
            p => !playersJoinedMidGame.Contains(PlayerPFID(p))
        );

        if (players.Count() < minPlayersForScoreUpdate) {
            Debug.Log($"[RankBalance.SavePlayerScores] NOTE: not updating scores because < {minPlayersForScoreUpdate} players played the full match");
            return;
        }

        foreach (Player p in players) {
            string pfid = PlayerPFID(p);
            if (pfid == "") {
                Debug.Log($"[RankBalance.SavePlayerScores] WARNING: player '{p.nick}' has no account ID");
                continue;
            }
            PubScore score = new PubScore(p.nick);
            if (pubscores.ContainsKey(pfid))
                score = PubScore.fromDict(p.nick, pubscores[pfid] as Dictionary<string, object>);
    
            score.AddMatch(MatchResult.fromCurrentMatch(p));
            pubscores[pfid] = score.toDict();
        }
        FileHelper.WriteJson(ScorePath(), pubscores);
    }

    class PubScore {
        public static float ScoreRecentMatchesWeight = 0.5f;
        public static int ScoreRecentMatches = 10;

        public string nick;
        public MatchResult total;
        private IList<MatchResult> lastMatches;

        public PubScore(string playernick) {
            nick = playernick;
            total = new MatchResult();
            lastMatches = new List<MatchResult>();
        }

        public static PubScore fromDict(string nick, Dictionary<string, object> obj) {
            var res = new PubScore(nick);
            res.total = MatchResult.fromDict(obj["total"] as Dictionary<string,object>);
            foreach (object m in (List<object>)obj["last_matches"])
                res.AddMatch(MatchResult.fromDict(m as Dictionary<string,object>), false);
            return res;
        }

        public Dictionary<string,object> toDict() {
            return new Dictionary<string,object>() {
                {"nick", nick},
                {"score",ComputeScore()},
                {"total",total.toDict()},
                {"last_matches",lastMatches.Take(PubScore.ScoreRecentMatches).Select(m => m.toDict()).ToList()},
            };
        }

        public void AddMatch(MatchResult m, bool addToTotal=true) {
            lastMatches.Insert(0, m);
            if (addToTotal)
                total.Add(m);
        }

        public float ComputeScore() {
            MatchResult sum = new MatchResult();
            foreach (MatchResult m in lastMatches.Take(PubScore.ScoreRecentMatches))
                sum.Add(m);
            
            MatchResult totalWithoutSum = new MatchResult();
            totalWithoutSum.Add(total);
            totalWithoutSum.Sub(sum);

            var w = PubScore.ScoreRecentMatchesWeight;
            return ((1f-w) * totalWithoutSum.ComputeScore()) + (w * sum.ComputeScore());
        }

        override public string ToString() {
            string matchinfo = lastMatches.Count() == PubScore.ScoreRecentMatches
                ? ""
                : $", only {lastMatches.Count()} recorded";
            return $"{ComputeScore():F2}\t\t{nick}\t({total.matches} matches{matchinfo})";
        }
    }

    class MatchResult {
        public static float ScoreWinrateWeight = 0.3f;
        public static float ScoreKdWeight = 0.3f;

        public int matches;
        public int match_score;
        public int kills;
        public int deaths;
        public int wins;
        public int loses;

        public static MatchResult fromCurrentMatch(Player p) {
            var stats = MonoSingleton<Stats>.Get.stats[p.id];
            var teams = Teams.instance.teams;
            bool isTie = teams[0].score == teams[1].score;
            int winningTeam = teams.OrderBy(t => t.score).Last().Number;
            bool isWinner = !isTie && p.GetTeam() == winningTeam;

            var res = new MatchResult();
            res.match_score = stats.score;
            res.matches = 1;
            res.kills   = stats.kills;
            res.deaths  = stats.deaths;
            res.wins    = !isTie && isWinner ? 1 : 0;
            res.loses   = !isTie && !isWinner ? 1 : 0;
            return res;
        }

        public static MatchResult fromDict(Dictionary<string, object> obj) {
            var res = new MatchResult();
            res.match_score = System.Convert.ToInt32(obj["match_score"]);
            res.matches = System.Convert.ToInt32(obj["matches"]);
            res.kills   = System.Convert.ToInt32(obj["kills"]);
            res.deaths  = System.Convert.ToInt32(obj["deaths"]);
            res.wins    = System.Convert.ToInt32(obj["wins"]);
            res.loses   = System.Convert.ToInt32(obj["loses"]);
            return res;
        }

        public Dictionary<string,object> toDict() {
            return new Dictionary<string,object>() {
                {"matches",matches},
                {"match_score",match_score},
                {"kills",kills},
                {"deaths",deaths},
                {"wins",wins},
                {"loses",loses},
            };
        }

        public void Add(MatchResult other) {
            match_score += other.match_score;
            matches += other.matches;
            kills   += other.kills;
            deaths  += other.deaths;
            wins    += other.wins;
            loses   += other.loses;
        }

        public void Sub(MatchResult other) {
            match_score -= other.match_score;
            matches -= other.matches;
            kills   -= other.kills;
            deaths  -= other.deaths;
            wins    -= other.wins;
            loses   -= other.loses;
        }

        public float ComputeScore() {
            if (matches == 0) return 0f;
            float scorePerMatch = (float)match_score / (float)matches;
            float winRate = (float)wins / (float)matches;
            float kd = deaths > 0f ? (float)kills / (float)deaths : 1f;
            var remainingWeight = 1f - MatchResult.ScoreWinrateWeight - MatchResult.ScoreKdWeight;
            return (remainingWeight * scorePerMatch) +
                (MatchResult.ScoreWinrateWeight * winRate * scorePerMatch) +
                (MatchResult.ScoreKdWeight * kd * scorePerMatch);
        }
    }

    // ↓ ↓ HELPERS ↓ ↓

    bool AssignTeam(Player p, int team, bool dryrun=false) {
        bool teamChanged = (int)p.props["team"] != team;
        if (!dryrun) {
            p.props["team"] = team;
            // balancing a player can result in a 'self'kill, costing 1 point..
            // FIXME: this is not the right way i guess - breaks assignment on matchstart/join.
            // if ((int)p.props["score"] = 0)
            // if (!p.controlled.dead && teamChanged)
            //     // MonoSingleton<Stats>.Get.stats[p.id].score++;
            //     p.props["score"] = (int)p.props["score"] + 1;
        }
        return teamChanged;
    }

    int NextTeam(int team) { return (team + 1) % 2; }

    bool TeamsTied() {
        var teams = GetComponent<Teams>().teams;
        return teams[0].score == teams[1].score;
    }

    int WinningTeam() {
        var teams = GetComponent<Teams>().teams;
        return teams.OrderBy(t => t.score).Last().Number;
    }

    float PlayerPubscore(Player p) {
        string pfid = PlayerPFID(p);
        if (!pubscores.ContainsKey(pfid)) return 0;
        var s = PubScore.fromDict(p.nick, pubscores[pfid] as Dictionary<string, object>);
        float score = s.ComputeScore();
        return s.ComputeScore();
    }

    float PlayerRank(Player p) {
        // rank is the MatchmakingAPI tier from 1-18.
        // if a player (or bot) is unranked, this returns 0 - to make these players count better, we increment the rank by 3.
        // NOTE: its not clear which rank this is.. probably CTF-Standard-6
        // FIXME: this value is set a couple seconds after a player has joined (API latency is bad...), it returns 0 in the meantime.
        //if (p.IsBot()) return 0f;
        return (float)(byte)p.props["rank"] + (p.IsBot() ? 0f : 3f);
    }

    bool IsAdmin(Player p) { 
        return (int)p.props["seclev"] != (int)Player.Seclev.Regular;
    }

    List<Player> GetHumansOfTeamExcept(int team, Player except) {
        return Players.Get.GetPlayersOfTeamExcept(team, except)
            .Where((Player p) => !p.IsBot()).ToList();
    }

    Player FindPlayerByName(string name) {
        return Players.Get.GetPlayersNonSpecators().Find(
            p => p.nick.ToLower().Contains(name.ToLower())
        );
    }

    string PlayerPFID (Player p) {
        return (string)p.props["account"];
    }

    string MatchGamemode () {
        return Scoreboard.Get.gamemodeText.text.ToLower().Replace(" ", "_");
    }

    string ScorePath () { return $"{basePath}/scores_by_mode/{MatchGamemode()}.json"; }
    string ConfigPath () { return $"{basePath}/config.json"; }
}
