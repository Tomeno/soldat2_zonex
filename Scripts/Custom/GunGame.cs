using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using Teal;

[DisallowMultipleComponent]
public class GunGame : MonoBehaviour
{
    Match match;
    Scoreboard scoreboard;
    SpawnLoadout spawnLoadout;

    int opt;

    //[JsonProperty("WeaponProgression")]
    public List<string> weaponProgression = new List<string>(new[]
    {
        "M79", "Barrett", "Rheinmetall", "RocketLauncher", "SteyrAUG", "Spas12", "Kalashnikov", "Dragunov", "MP5", "Deagles", "RPG", "Makarov", "Knife"
    });

    void Start()
    {
        //Debug.Log("!!!![GunGame.Start]!!!!");
        scoreboard = Scoreboard.Get;
        match = GetComponent<Match>();
        match.effects.sayScoreAnnouncements = true;
        match.effects.sayKillsAnnouncements = true;
        match.scoreLimit = weaponProgression.Count;
        spawnLoadout = GetComponent<SpawnLoadout>();
        spawnLoadout.randomIfNotAvailable = false;
        spawnLoadout.grenadesMax = 0;
        spawnLoadout.grenades = 0;
        var avail = Available.instance;
        avail.primaryWeapons.Clear();
        /*if (Net.IsServer)
            avail.primaryWeapons.AddRange(weaponProgression);
        else
            avail.primaryWeapons.Add(weaponProgression[0]);*/
        avail.secondaryWeapons.Clear();
        Debug.Log("[GunGame.Start] Set up GunGame");
    }

    /*void Awake()
    {
        Debug.Log("!!!![GunGame.Awake]!!!!");
        scoreboard = Scoreboard.Get;
        match = GetComponent<Match>();
        match.effects.sayScoreAnnouncements = true;
        match.effects.sayKillsAnnouncements = true;
        match.scoreLimit = weaponProgression.Count;
        spawnLoadout = GetComponent<SpawnLoadout>();
        spawnLoadout.randomIfNotAvailable = false;
        spawnLoadout.grenadesMax = 0;
        spawnLoadout.grenades = 0;
        var avail = Available.instance;
        avail.primaryWeapons.Clear();
        if (Net.IsServer)
            avail.primaryWeapons.AddRange(weaponProgression);
        else
            avail.primaryWeapons.Add(weaponProgression[0]);
        avail.secondaryWeapons.Clear();
        Debug.Log("[GunGame.Awake] Set up GunGame");
    }*/

    void FixedUpdate()
    {
        if (opt++ % 10 == 1)
            LazyUpdate();

        if (Net.IsServer)
        {
            foreach (var p in Players.Get.players)
                UpdatePlayerWeapon(p);
        }
    }
    
    void Master_GiveWeapon(GameObject gostek, string wepName)
    {
        if (!Net.IsServer)
            return;

        ItemPickup itemPickup = gostek.GetComponent<ItemPickup>();
        itemPickup.DestroyAllWithTag("Weapon"); // destroy all current gostek weapons
        
        Action<GameObject> InitWeapon = delegate(GameObject newObject)
        {
            ItemPickup pickup = gostek.GetComponent<ItemPickup>();
            if (pickup)
                pickup.Pickup(newObject.GetComponent<Item>(), null);
            Weapon weapon = newObject.GetComponent<Weapon>();
            if (weapon)
            {
                weapon.ammoCount = weapon.clipCount * 9999999;
                bool flag9 = !weapon.startLoaded && weapon.ammoCount == 0;
                if (flag9)
                {
                    weapon.ammoCount = weapon.clipCount;
                }
            }
        };

        // create a new weapon and sync its creation
        Prefabs.Get.Master_InstantiateOnNetwork(new InstantiateParams {
            prefabName = wepName,
            preSendInit = InitWeapon
        });
    }

    void UpdatePlayerWeapon(Player player)
    {
        var wep = GetWeaponForScore(player.Int("score"));
        var oldWep = player.String("weapon");
        //if (wep == player.String("weapon"))
        //    return;
        //Debug.Log("SET WEAPON FOR " + player + " to " + wep + " from " + player.String("weapon"));
        Eventor.Publish(Events.Weapon_Selected, new GlobalWeaponSelectEvent(base.gameObject, player, wep, ""));
        if (wep == oldWep)
            return;
        player.props["weapon"] = wep;
        player.props["weapon2"] = "";
        if (!player.IsDead() && player.controlled && player.controlled.gameObject)
        {
            Master_GiveWeapon(player.controlled.gameObject, wep);
        }
    }

    string GetWeaponForScore(int score)
    {
        var idx = Math.Max(0, Math.Min(weaponProgression.Count - 1, score));
        return weaponProgression[idx];
    }

    void LazyUpdate() // update 10 times a second
    {
        if (Net.IsClient && match.showStandardStatus)
            Client_Update();
    }

    void Client_Update()
    {
        string status;
        if (match.WaitingOnPause())
        {
            status = $"Waiting for the match to start...";
            HUD.Get.Status(status);
        }
        else
        if (match.IsWaitingForPlayers())
        {
            int amount = match.minimumPlayers - scoreboard.sortedPlayers.Count;
            string players = amount > 1 ? "players" : "player";
            status = $"Waiting for {amount} more {players}...";
            HUD.Get.Status(status);
        }

        if (scoreboard.sortedPlayers.Count < 2)
            return;

        // display status with score

        status = "<size=75%>" + scoreboard.timeText.text + "</size>";
        status += " ";
        Player local = Players.Get.GetLocalPlayer();
        Player lead = scoreboard.sortedPlayers[0];
        Player second = scoreboard.sortedPlayers[1];
        if (local == null || lead == null || second == null) // exiting to menu?
            return;

        int leadscore = lead.Int("score");
        int localscore = local.Int("score");

        string wep = GetWeaponForScore(localscore);
        string nextWep = GetWeaponForScore(localscore + 1);
        local.props["weapon"] = wep;
        local.props["weapon2"] = "";

        if (match.scoreLimit > 0)
        {
            status += " <color=red>" + localscore + "</color><size=75%>/" + match.GetScaledScoreLimit() + " ";
        }

        if (leadscore > 0)
        {
            int pointsToLead = leadscore - localscore;
            int pointsToSecond = leadscore - second.Int("score");

            if (pointsToLead == 0)
            {
                if (local == lead && pointsToSecond > 0)
                {
                    status += " leading";
                    status += " +" + pointsToSecond + "";
                }
                else
                {
                    status += " tied for the lead";
                }
            }
            else
            {
                status += " losing" + " -" + pointsToLead;
            }
            status += " <color=blue>Next: " + nextWep + "</color>";
            status += "</size>";
        }

        HUD.Get.Status(status);
    }
}
