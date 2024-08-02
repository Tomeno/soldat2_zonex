/*
  Infiltration mode
    Edited from CTF By Tomeno | 2024-07-24
*/

using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Teal;
using UberAudio;

[RequireComponent(typeof(Match))]
[RequireComponent(typeof(Teams))]
[DisallowMultipleComponent]
// ReSharper disable once CheckNamespace
public class Infiltration : MonoBehaviour
{
    [JsonProperty("CaptureScore")] public int captureScore = 10;
    [JsonProperty("CaptureTeamScore")] public int captureTeamScore = 30;
    [JsonProperty("TimeOutFlagSecs")] public float timeOutFlagSecs = 25.0f;
    [JsonProperty("SafeFlagTimer")] public float safeFlagTimer = 5.0f; // how many seconds of flag safety for points
    [JsonProperty("SafeFlagPoints")] public int safeFlagPoints = 1; // how many points to give on the timer
    [JsonProperty("SafeFlagTimerReset")] public bool safeFlagTimerReset = true; // reset the timer when the flag is stolen
    [JsonProperty("AsymmetricMapHelper")] public bool asymmetricMapHelper = true;
    public bool captureSound, returnSound;

    Match match;
    Teams teams;
    Scoreboard scoreboard;
    HandleBases handleBases;

    GameObject ctfhud, captureEffectPrefab;

    List<Flag> flags = new List<Flag>();
	Flag bravoFlag = null;
    List<BaseCapture> bases = new List<BaseCapture>();

    const float FlagScale = 0.75f;
    const float triggerGuardSecs = 0.2f;
    float lastFlagSoundTime;
	float pointTimer;

    int opt;
    private static readonly int FlagColor = Shader.PropertyToID("_Color");

    private void Awake()
    {
        Eventor.AddListener(Events.Created, OnCreateGameObject);
        Eventor.AddListener(Events.Destroyed, OnDestroyedGameObject);
        Eventor.AddListener(Events.Flag_Captured, OnFlagCaptured);
        Eventor.AddListener(Events.Flag_Returned, OnFlagReturned);
        Eventor.AddListener(Events.Flag_Grabbed, OnFlagGrabbed);
        Eventor.AddListener(Events.End_Condition, OnEndCondition);
    }

    void Start()
    {
        match = GetComponent<Match>();
        teams = GetComponent<Teams>();
        handleBases = GetComponent<HandleBases>();
        scoreboard = Scoreboard.Get;
        // create interface
        ctfhud = Instantiate(Resources.Load<GameObject>("CTFInterface"), transform); // TODO: make this moddable
        captureEffectPrefab = Resources.Load<GameObject>("CaptureEffect");

        foreach (BaseCapture bc in BaseCapture._bases)
            AddBase(bc);
        foreach (Flag flag in Flag._flags)
            AddFlag(flag);

        match.effects.sayFlagAnnouncements = true;
        match.effects.sayScoreAnnouncements = true;
    }

    private void OnDestroy()
    {
        Eventor.RemoveListener(Events.Created, OnCreateGameObject);
        Eventor.RemoveListener(Events.Destroyed, OnDestroyedGameObject);
        Eventor.RemoveListener(Events.Flag_Captured, OnFlagCaptured);
        Eventor.RemoveListener(Events.Flag_Returned, OnFlagReturned);
        Eventor.RemoveListener(Events.Flag_Grabbed, OnFlagGrabbed);
        Eventor.RemoveListener(Events.End_Condition, OnEndCondition);
    }

    void FixedUpdate()
    {
        if (opt++ % 10 == 1)
            LazyUpdate();
		TickPoints();
    }

    void LazyUpdate() // update 10 times a second
    {
        if (Net.IsClient && match.showStandardStatus)
            Client_Update();

        if (!match.IsState(MatchState.InProgress) || match.scoreLimit == 0 || match.IsWaitingForPlayers())
            return;

        if (Net.IsServer)
            Master_ManageFlags();
    }

    void TickPoints()
    {
        /*if (!Net.IsServer) // no idea when this is necessary - T
            return;*/
		
        if (!match.IsState(MatchState.InProgress) || match.IsWaitingForPlayers() || match.WaitingOnPause())
            return;
		
		foreach (Flag flag in Flag._flags) {
			flag.render.material.SetColor("_Color", GetFlagColor(flag));
			if (flag.team.Number == 0) { // Bravo's flag
				if (flag.inbase) {
					pointTimer += Time.fixedDeltaTime;
				} else if (safeFlagTimerReset) {
					pointTimer = 0f;
				}
				if (pointTimer >= safeFlagTimer) {
					pointTimer -= safeFlagTimer;
					teams.teams[0].score += safeFlagPoints;
				}
				break;
			}
		}
    }

    void Client_Update()
    {
        string status = "<size=75%>" + scoreboard.timeText.text + "</size>";

        if (match.WaitingOnPause())
        {
            status = $"Waiting for the match to start...";
        }
        else if (match.IsWaitingForPlayers())
        {
            int amount = match.minimumPlayers - scoreboard.sortedPlayers.Count;
            string players = amount > 1 ? "players" : "player";
            status = $"Waiting for {amount} more {players}...";
        }

        if (match.GetScaledMatchSecs() > 0 || match.IsWaitingForPlayers() || match.WaitingOnPause())
            HUD.Get.Status(status);
    }

    void Master_ManageFlags()
    {
        for (int i = flags.Count - 1; i >= 0; i--)
            if (flags[i] == null)
                flags.RemoveAt(i);

        for (int i = bases.Count - 1; i >= 0; i--)
            if (bases[i] == null)
                bases.RemoveAt(i);

        for (int i = 0; i < bases.Count; i++)
        {
            BaseCapture bs = bases[i];
            if (bs && bs.flag == null)
            {
                bs.Master_CreateFlag();
            }
        }

        for (int i = 0; i < flags.Count; i++)
        {
            Flag flag = flags[i];
            if (flag && flag.transform.parent == null)
            {
                if (flag.looseTime > timeOutFlagSecs - 5f)
                    flag.blink = true;
				
				if (flag.team.Number == 1) { // Alpha's flag should be immovable
					flag.body.mass = 99999999;
					flag.body.isKinematic = false;
					flag.body.position = flag.basePoint;
				}
                
                if (flag.looseTime > timeOutFlagSecs)
                    flag.Master_Return();
            }
        }

        if (flags.Count > 2)
        {
            Debug.Log("Too many flags " + flags.Count);
            Destroy(flags[flags.Count - 1].gameObject);
            RemoveFlag(flags[flags.Count-1]);
        }
    }

    // events

    void OnFlagCaptured(IGameEvent ev)
    {
        GlobalFlagEvent gfe = ev as GlobalFlagEvent;
        Flag flag = gfe.Sender.GetComponent<Flag>();

        // score

        Team flagTeam = gfe.Sender.GetComponent<Team>();
        int scoreTeam = (flagTeam.Number + 1) % 2;
        if (match.IsState(MatchState.InProgress))
            teams.teams[scoreTeam].score += captureTeamScore;
        if (GameSettings.instance.ShowEventTexts)
            HUD.Get.Notify(teams.teams[scoreTeam].name + " team scores", teams.teams[scoreTeam].uicolor);
        Log(ev.Sender, "captured by");

        if (scoreTeam < bases.Count && flagTeam.Number < bases.Count)
        {
            Effect(bases.ElementAt(scoreTeam).gameObject, captureEffectPrefab);
            Effect(bases.ElementAt(flagTeam.Number).gameObject, captureEffectPrefab);
        }
        else
            Debug.LogWarning("Something wrong with bases. Count " + bases.Count + " scoreTeam " + scoreTeam);

        // add score

        Controls carrierControls = flag.GetComponentInParent<Controls>();
        if (carrierControls && carrierControls.player)
        {
            carrierControls.player.props["score"] = carrierControls.player.Int("score") + captureScore;
        }

        flag.Return();

        // STATUS

        bool isEnd = (match.GetScaledScoreLimit() > 0 && teams.teams[scoreTeam].score >= match.GetScaledScoreLimit());
        if (!isEnd)
        {
            Player local = Players.Get.GetLocalPlayer();
            if (local && !local.IsSpectator())
            {
                if (local && local.GetTeam() != flag.team.Number)
                    AudioManager.Instance.Play("Flag:Captured");
                else
                    AudioManager.Instance.Play("Flag:Lost");
            }

            //if (local && carrierControls && local.GetTeam() == carrierControls.player.GetTeam())
            //{
            //    match.effects.AnnouncerSayGuaranteed("Announcer:Score");
            //else
            if (local == null || carrierControls == null || !carrierControls.IsLocal())
                match.effects.AnnouncerSayGuaranteed(flag.team.Number == 0 ? "Announcer:RedScores" : "Announcer:BlueScores");
        }


        if (carrierControls && carrierControls.player)
        {
            KillStatus.Get.SetStatus(carrierControls.player, null, KillStatus.Get.staySecs, "CAPTURED BY", GetFlagColor(gfe.Sender), isEnd ? KillStatus.Style.End : KillStatus.Style.Event);
            KillLog.instance.AddFlag(carrierControls.player, flagTeam.Number, KillLog.FlagType.Cap);
        }
    }

    void OnFlagGrabbed(IGameEvent ev)
    {
        Flag flag = ev.Sender.GetComponentInChildren<Flag>();
        Player local = Players.Get.GetLocalPlayer();
        Player carrier = GetFlagHolderPlayer(ev.Sender);

        if (flag.inbase)
        {
            AudioManager.Instance.Play("Flag:Grabbed", ev.Sender.transform.position);

            //if (local && carrier && local.GetTeam() != carrier.GetTeam())
            //    match.effects.AnnouncerSayGuaranteed("Announcer:FlagLost");
            //else
            //if (local == null || carrier == null || carrier != local)

            lastFlagSoundTime = Time.time;

            if (GameSettings.instance.ShowEventTexts)
                HUD.Get.Notify(GetFlagName(ev.Sender) + " taken", GetFlagColor(ev.Sender, true));
            if (local && Vector3.Distance(flag.transform.position, local.controlled ? local.controlled.transform.position : Vector3.zero) < 5.0f && flag.team.Number != local.GetTeam())
                HUD.Get.SetDirection(handleBases.reverseFlags ? -flag.GetTeamDirection() : flag.GetTeamDirection(), 2.5f);
            flag.safe = false;
            Log(ev.Sender, "taken by");
        }
        else
        {
            AudioManager.Instance.Play("Flag:Grabbed:Short", ev.Sender.transform.position);
        }

        if ((flag.inbase || flag.dropped) && !handleBases.reverseFlags)
        {
            if (local && carrier && !local.IsSpectator())
                match.effects.AnnouncerSayGuaranteed(local.GetTeam() != carrier.GetTeam() ? "Announcer:EnemyHasFlag" : "Announcer:TeamHasFlag");
            else
                match.effects.AnnouncerSayGuaranteed(flag.team.Number == 0 ? "Announcer:RedHasFlag" : "Announcer:BlueHasFlag");
        }

        if (carrier && flag.inbase)
        {
            KillLog.instance.AddFlag(carrier, flag.team.Number, KillLog.FlagType.Taken);
        }

        flag.grabbed = true;
    }

    void OnFlagReturned(IGameEvent ev)
    {
        Flag flag = ev.Sender.GetComponentInChildren<Flag>();
        Player local = Players.Get.GetLocalPlayer();

        if (flag.grabbed)
        {
            AudioManager.Instance.Play("Flag:Returned", ev.Sender.transform.position);
            if (local && !local.IsSpectator())
                match.effects.AnnouncerSayGuaranteed(local.GetTeam() == flag.team.Number ? "Announcer:TeamReturnedFlag" : "Announcer:EnemyReturnedFlag");
            else
                match.effects.AnnouncerSayGuaranteed(flag.team.Number == 0 ? "Announcer:BlueReturnedFlag" : "Announcer:RedReturnedFlag");

            lastFlagSoundTime = Time.time;
        }
        else
            AudioManager.Instance.Play("Flag:Returned:Short", ev.Sender.transform.position);

        if (GameSettings.instance.ShowEventTexts)
            HUD.Get.Notify(GetFlagName(ev.Sender) + " returned", GetFlagColor(ev.Sender, false));

        Effect(ev.Sender, captureEffectPrefab);


        Log(ev.Sender, flag.lastTouchedPlayer, "returned by");
        flag.Return();

        if (flag.lastTouchedPlayer)
        {
            KillLog.instance.AddFlag(flag.lastTouchedPlayer, flag.team.Number, KillLog.FlagType.Returned);
        }
    }

    void OnFlagTouched(GameObject player, GameObject other)
    {
        if (!Net.IsServer)
            return;
        if (match.IsState(MatchState.Ended) || match.IsWaitingForPlayers() || match.WaitingOnPause())
            return;

        Flag flag = other.GetComponentInParent<Flag>();
        Team playerTeam = player.GetComponent<Team>();
        StandardObject playerStandard = player.GetComponent<StandardObject>();

        if (flag)
        {
            flag.lastTouchedPlayer = player.GetComponent<Controls>().player;
        }

        if (flag && !flag.carried && flag.Capturable && playerTeam && playerStandard && !playerStandard.Dead && playerStandard.GetTimeSinceCreated() > 1.2f // guard
           && flag.GetComponent<Item>().CanBePicked(player, 0.75f)
           )
        {
            if (playerTeam.Number != flag.team.Number && flag.team.Number == 0) // can only steal Bravo's flag
            {
                ItemPickup pickup = player.GetComponent<ItemPickup>();
                if (pickup && RealTime.realtimeSinceStartup - flag.triggerGuardTime > triggerGuardSecs)
                {
                    pickup.Master_Pickup(flag.gameObject);
                    flag.triggerGuardTime = RealTime.realtimeSinceStartup;
                }
            }
            else // my flag
            {
                if (flag.inbase) // only cap on Alpha's flag
                {
					if (flag.team.Number == 1) {
						Flag carriedFlag = Flag.GetFlagFromCarrier(player);
						if (carriedFlag && RealTime.realtimeSinceStartup - carriedFlag.triggerGuardTime > triggerGuardSecs)
						{
							carriedFlag.Master_Capture(flag);
							flag.triggerGuardTime = RealTime.realtimeSinceStartup;
						}
					}
                }
                else if (RealTime.realtimeSinceStartup - flag.triggerGuardTime > triggerGuardSecs) // return to base
                {
                    flag.Master_Return();
                    flag.triggerGuardTime = RealTime.realtimeSinceStartup;
                }
            }
        }
    }

    void OnCreateGameObject(IGameEvent ev)
    {
        Flag flag = ev.Sender.GetComponent<Flag>();
        if (flag)
        {
            AddFlag(flag);
        }

        BaseCapture bs = ev.Sender.GetComponent<BaseCapture>();
        if (bs)
        {
            AddBase(bs);
        }
    }

    void OnDestroyedGameObject(IGameEvent ev)
    {
        Flag flag = ev.Sender.GetComponent<Flag>();
        if (flag)
        {
            RemoveFlag(flag);
        }

        BaseCapture bs = ev.Sender.GetComponent<BaseCapture>();
        if (bs)
        {
            RemoveBase(bs);
        }
    }    

    void AddFlag(Flag flag)
    {
        if (flag == null || flags.Contains(flag) || flag.GetComponent<NetworkObject>() == null)
            return;
        Debug.Log("INF OnCreateGameObject FLAG " + flag.GetComponent<NetworkObject>().Id + " " + flag.baseCapture);
        flags.Add(flag);
        flag.AddTouchedEvent(OnFlagTouched);
        flag.transform.localScale = new Vector3(FlagScale, FlagScale, FlagScale);
        flag.render.material.SetColor(FlagColor, GetFlagColor(flag));
    }

    void RemoveFlag(Flag flag)
    {
        if (flags.Remove(flag))
        {
            Debug.Log("removed flag " + flag.GetComponent<NetworkObject>().Id + " " + flag.baseCapture);
            flag.RemoveTouchedEvent(OnFlagTouched);
            flag.transform.localScale = Vector3.one;
        }
    }

    void AddBase(BaseCapture bc)
    {
        if (bc == null || bases.Contains(bc) || bc.GetComponent<NetworkObject>() == null)
            return;
        /*Debug.Log("CTF OnCreateGameObject Base " + bc.GetComponent<NetworkObject>().Id + " " + bc.flag);
        Debug.Log("BaseCapture position " + bc.transform.position);
        Debug.Log("BaseCapture team " + bc.team);
		if (System.Math.Abs(bc.transform.position.z) < 0.000001) {
			bc.team.Number = 1;
		}
        Debug.Log("BaseCapture team post " + bc.team);*/
        bases.Add(bc);
        // base has a flag and respawns by default - remove them
        if (Net.IsServer) // dont null on client
            bc.DestroyFlag();
        bc.SetRespawns(false);
    }

    void RemoveBase(BaseCapture bc)
    {
        if (bases.Remove(bc))
            Debug.Log("removed base " + bc.GetComponent<NetworkObject>().Id + " " + bc.flag);        
    }

    // other

    public static void Effect(GameObject flagObject, GameObject captureEffectPrefab)
    {
        GameObject effect = Instantiate(captureEffectPrefab);
        effect.transform.position = flagObject.transform.position + Vector3.up * 3.0f;
        Color flagColor = GetFlagColor(flagObject);
        Light light = effect.gameObject.GetComponentInChildren<Light>();
        if (light)
        {
            light.color = flagColor;
        }
        foreach (ParticleSystem ps in effect.GetComponentsInChildren<ParticleSystem>())
        {
           ParticleSystem.MainModule m = ps.main;
           m.startColor = flagColor;
        }
        Destroy(effect, 4.0f);
    }

    void OnEndCondition(IGameEvent e)
    {
        if (!enabled || ctfhud == null)
            return;

        ctfhud.SetActive(false);
    }

    // helper functions

    void Log(GameObject flagObject, string text)
    {
        Log(flagObject, GetFlagHolderPlayer(flagObject), text);
    }

    void Log(GameObject flagObject, Player carrier, string text)
    {
        if (carrier)
            Debug.Log(GetFlagName(flagObject) + " " + text + " " + carrier.nick + " [" + carrier.String("account") + "] (" + carrier.Int("team") + ")");
    }

    Player GetFlagHolderPlayer(GameObject flagObject)
    {
        Flag flag = flagObject.GetComponent<Flag>();
        ItemPickup holder = flag.lastholder;
        if (holder)
        {
            return holder.GetComponent<Controls>().player;
        }
        return null;
    }

    string GetFlagName(GameObject flagObject)
    {
        int flagTeam = flagObject.GetComponent<Team>().Number;
        return (flagTeam == 0 ? "Bravo" : "Alpha") + " flag";
    }

    public static Color GetFlagColor(GameObject flagObject, bool otherTeam = false)
    {
        return flagObject.GetComponent<Team>().Number == 0 ? new Color(0.2f, 0.2f, 0.2f, 1f) : new Color(0.8f, 0.8f, 0.8f, 1f);
        //return Teams.instance.teams[(flagObject.GetComponent<Team>().Number + (otherTeam ? 1 : 0)) % 2].uicolor;
    }

    public static Color GetFlagColor(Flag flag, bool otherTeam = false)
    {
        return flag.team.Number == 0 ? new Color(0.2f, 0.2f, 0.2f, 1f) : new Color(0.8f, 0.8f, 0.8f, 1f);
        //return Teams.instance.teams[(flag.GetComponent<Team>().Number + (otherTeam ? 1 : 0)) % 2].uicolor;
    }
}