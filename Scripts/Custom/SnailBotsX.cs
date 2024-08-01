/**
 * AimBotsfor Soldat 2
 * - Make bots rek people?
 *
 * Soldat 2 Community License:
 *
 * You are allowed to redistribute or use the software in source or binary form
 * with or without modifications provided that you share your modifications
 * with the Soldat 2 community in source form. 
 **/

using UnityEngine;
using Teal;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Diagnostics;



public class SnailBots: MonoBehaviour
{
	class Waypoint {
		public static List<Waypoint> _allWaypoints = new List<Waypoint>();
		public static List<Waypoint> _allWaypoints2 = new List<Waypoint>();
		
		public bool visited = false;
		public Vector2 point = new Vector2(0,0);
		private RaycastHit2D[] raycastResults = new RaycastHit2D[2];
		public List<Waypoint> neighbours = new List<Waypoint>();
		private static System.Random rand = new System.Random();
		public float dir = 0.0f;
		public static int raycastLayer = LayerMask.GetMask(new string[]{"Gostek","Ground"});
		
		public Waypoint(Vector2 point) {
			this.point = point;
			this.visited = false;
			this.dir = 0.0f;
		}
		
		public Waypoint(Vector2 point, float dir) {
			this.point = point;
			this.visited = false;
			this.dir = dir;
		}

		
		public void TryAddNeighbour(Waypoint neighbour) {
			Vector2 toNeighbour = neighbour.point - point;
			int hits = Physics2D.RaycastNonAlloc(point, toNeighbour, raycastResults, toNeighbour.magnitude, raycastLayer);
			if(hits == 0) 
				neighbours.Add(neighbour);
		}
		
		private bool CanSeePoint(Vector2 thepoint) {
			Vector2 toPoint = thepoint - this.point;
			int hits = Physics2D.RaycastNonAlloc(point, toPoint, raycastResults, toPoint.magnitude, raycastLayer);
			if(hits > 0) {
				return false;
			}
			return true;
		}
		
		public static void ComputeNeighbours() {
			foreach(Waypoint wp in _allWaypoints) {
				foreach(Waypoint wp2 in _allWaypoints) {
					wp.TryAddNeighbour(wp2);
				}
			}
		}
		
		public static void GenerateWaypoints() {
			_allWaypoints.Clear();
			_allWaypoints2.Clear();
			
			foreach (PropModel pm in GameObject.FindObjectsOfType<PropModel>()) {
				if (pm.mapData.model == "SM_Prop_Sign_01") {
					_allWaypoints.Add(new Waypoint((Vector2)pm.transform.position));
				} /*else if (pm.mapData.model == "SM_Prop_Sign_Stop_01") {
					_allWaypoints2.Add(new Waypoint((Vector2)pm.transform.position, 1.0f));
				}*/
			}
			
			if(_allWaypoints.Count < 4) {
				GenerateWaypointsEx();
			}
		}
		
		public static void GenerateWaypointsEx() {
			int maxX = (int)(Map.Get.scale.x*2*Map.Get.width*7);
			int maxY = (int)(Map.Get.scale.y*Map.Get.height*7);
			
			Collider2D[] results = new Collider2D[1];
			int i = 0;
			while(i < 48) {
				int x = rand.Next(maxX);
				int y = rand.Next(maxY);
				
				int hits = Physics2D.OverlapPointNonAlloc(new Vector2(x, y), results, raycastLayer);
				
				if(hits < 1) {
					_allWaypoints.Add(new Waypoint(new Vector2(x,y)));
					i++;
				}
			}
		}
		
		public static void HideWaypoints() {
			foreach(PropModel pm in GameObject.FindObjectsOfType<PropModel>()) {
				if(pm.mapData.model == "SM_Prop_Sign_01")
					pm.transform.position = new Vector3(-10, -10, 0); // move it out of the way :)
			}
		}
		
		public static Waypoint FindHighestVisible(Vector2 vec) {
			Waypoint bestWaypoint = null;
			
			foreach(Waypoint wp in _allWaypoints) {
				if(wp.CanSeePoint(vec)) {
					if(bestWaypoint == null)
						bestWaypoint = wp;
					else {
						if(wp.point.y > bestWaypoint.point.y)
							bestWaypoint = wp;
					}
				}
			}
			
			return bestWaypoint;
		}
		
		public static Waypoint FindLowestVisible(Vector2 vec) {
			Waypoint bestWaypoint = null;
			
			foreach(Waypoint wp in _allWaypoints) {
				if(wp.CanSeePoint(vec)) {
					if(bestWaypoint == null)
						bestWaypoint = wp;
					else {
						if(wp.point.y < bestWaypoint.point.y)
							bestWaypoint = wp;
					}
				}
			}
			
			return bestWaypoint;
		}
		
		public static Waypoint FindVisibleWaypoint(Vector2 vec, Vector2 vec2, float dir) {
			SnailBot.lineReached = 1000;
			Waypoint bestWaypointX = null;
			
			if(_allWaypoints2.Count != 0) {
				SnailBot.lineReached = 1001;
				foreach(Waypoint wp2 in _allWaypoints2) {
					SnailBot.lineReached = 1002;
					if(dir > 0) {
						if(wp2.dir < 0)
							continue;
						
						if(bestWaypointX == null) {
							if(wp2.CanSeePoint(vec))
								bestWaypointX = wp2;
						}
						else {
							if(bestWaypointX.point.x > wp2.point.x)
								if(wp2.CanSeePoint(vec))
									bestWaypointX = wp2;
						}
					} else {
						if(wp2.dir > 0)
							continue;
						
						if(bestWaypointX == null) {
							if(wp2.CanSeePoint(vec))
								bestWaypointX = wp2;
						}
						else {
							if(bestWaypointX.point.x < wp2.point.x)
								if(wp2.CanSeePoint(vec))
									bestWaypointX = wp2;
						}
					}
				}
				
				SnailBot.lineReached = 1500;
				
				if(bestWaypointX != null) {
					return bestWaypointX;
				}
			}
			
			Waypoint bestWaypoint = null;
			Waypoint perfectWaypoint = null;
			
			foreach(Waypoint wp in _allWaypoints) {
				if(wp.CanSeePoint(vec)) {
					if(wp.CanSeePoint(vec2)) {
						if(perfectWaypoint == null) {
							perfectWaypoint = wp;
						} else {
							if(dir > 0) {
								if (wp.point.x > perfectWaypoint.point.x)
									perfectWaypoint = wp;
							} else {
								if (wp.point.x < perfectWaypoint.point.x)
									perfectWaypoint = wp;
							}
						}
					} else {
						if(bestWaypoint == null) {
							bestWaypoint = wp;
						} else {
							if (dir > 0) {
								if (wp.point.x > bestWaypoint.point.x)
									bestWaypoint = wp;
							} else {
								if (wp.point.x < bestWaypoint.point.x)
									bestWaypoint = wp;
							}
						}
					}
				}
			}
			
			if(perfectWaypoint != null)
				return perfectWaypoint;
			
			
			return bestWaypoint;
		}
		
		public static void InitializePathFinding() {
			GenerateWaypoints();
			ComputeNeighbours();
			HideWaypoints();
		}
		
		public static Waypoint GetRandom() {
			if(_allWaypoints.Count == 0)
				return null;
			
			return _allWaypoints[rand.Next(_allWaypoints.Count)];
		}
		
		public static Queue<Waypoint> FindPath(Vector2 vFrom, Vector2 vTo) {
			// Find a start and end point
			Waypoint wpStart = null;
			Waypoint wpEnd = null;
			
			foreach(Waypoint wp in _allWaypoints) {
				wp.visited = false;
			}
			
			foreach(Waypoint wp in _allWaypoints) {
				if (wp.CanSeePoint(vTo)) {
					wpStart = wp;
					break;
				}
			}
			
			foreach(Waypoint wp in _allWaypoints) {
				if (wp.CanSeePoint(vFrom)) {
					wpEnd = wp;
					break;
				}
			}
			
			if (wpStart == null) {
				GameChat.ChatOrLog("{PATH FINDING COULD NOT LOCATE START POINT}");
			} else {
				GameChat.ChatOrLog("{START POINT FOUND AT " + wpStart.point.ToString() + "}");
			}
			
			if (wpEnd == null) {
				GameChat.ChatOrLog("{PATH FINDING COULD NOT LOCATE END POINT}");
			} else {
				GameChat.ChatOrLog("{END POINT FOUND AT " + wpEnd.point.ToString() + "}");
			}
			
			Queue<Waypoint> path = new Queue<Waypoint>();
			
			if(PathFromTo(wpStart, wpEnd, path)) {
				foreach(Waypoint wp in path) {
					GameChat.ChatOrLog("{WP: " + wp.point.ToString() + "}");
				}
				return path;
			}
				
			return null;
		}
		
		private static bool PathFromTo(Waypoint wpStart, Waypoint wpEnd, Queue<Waypoint> path) {
			if(wpStart.visited)
				return false;
			
			wpStart.visited = true;
			
			foreach(Waypoint neighbour in wpStart.neighbours) {
				if(neighbour == wpEnd) {
					GameChat.ChatOrLog("ROUTE FOUND!");
					path.Enqueue(wpEnd);
					return true;
				} else {
					if (PathFromTo(neighbour, wpEnd, path)) {
						path.Enqueue(wpStart);
						return true;
					}
				}
			}
			
			return false;
		}
	}
	
	class SnailBot {
		const int IDX_RIGHT = 0;
		const int IDX_LEFT = 1;
		const int IDX_DOWN = 2;
		const int IDX_UP = 3;
		const int IDX_DOWN_RIGHT = 4;
		const int IDX_DOWN_LEFT = 5;
		const int IDX_UP_RIGHT = 6;
		const int IDX_UP_LEFT = 7;
		const int IDX_UP_LEFT2 = 8;
		const int IDX_UP_RIGHT2 = 9;
		const int IDX_DOWN_LEFT2 = 10;
		const int IDX_DOWN_RIGHT2 = 11;
		
		
		const int IDX_GOSTEK_JET = 12;
		const int IDX_GOSTEK_JUMP = 13;
		const int IDX_GOSTEK_CAN_ROLL = 14;
		const int IDX_GOSTEK_JUMP_TIME = 15;
		const int IDX_GOSTEK_GROUNDED = 16;
		const int IDX_GOSTEK_SUPERMAN = 17;
		const int IDX_GOSTEK_VELX = 18;
		const int IDX_GOSTEK_VELY = 19;
		
		const int IDX_GOSTEK_SLOPE_LEFT = 20;
		const int IDX_GOSTEK_SLOPE_RIGHT = 21;
		
		
		const int IDX_OUT_JUMP = 0;
		const int IDX_OUT_SMAN = 1;
		const int IDX_OUT_JETS = 2;
		const int IDX_OUT_HAXS = 3;
		const int IDX_OUT_AIMX = 4;
		const int IDX_OUT_AIMY = 5;
		const int IDX_OUT_FIRE = 6;
		
		private float[] values = new float[22];
		private float[] outputs = new float[7];
		
		
		// Basic vectors!
		private Vector2 V_RIGHT = new Vector2(1, 0);
		private Vector2 V_LEFT = new Vector2(-1, 0);
		private Vector2 V_UP = new Vector2(0, 1);
		private Vector2 V_DOWN = new Vector2(0, -1);
		
		// More vectors!
		private Vector2 V_DOWN_RIGHT = new Vector2(1, -1);
		private Vector2 V_UP_RIGHT = new Vector2(1, 1);
		private Vector2 V_DOWN_LEFT = new Vector2(-1, -1);
		private Vector2 V_UP_LEFT = new Vector2(-1, 1);
		
		// And more
		private Vector2 V_DOWN_RIGHT2 = new Vector2(2, -1);
		private Vector2 V_DOWN_LEFT2 = new Vector2(-2, -1);
		private Vector2 V_UP_LEFT2 = new Vector2(-2, 1);
		private Vector2 V_UP_RIGHT2 = new Vector2(2, 1);
		
		private string[] valueNames = new string[22];
		private string[] outputNames = new string[7];
		
		private RaycastHit2D[] raycastResults = new RaycastHit2D[2];
		
		private Vector2[] raycastVectors = new Vector2[12];
		
		private bool wasJumping = false;
		private Stopwatch sw = new Stopwatch();
		
		public Player plrToControl = null;
		private Player plrToKill = null;
		
		private Flag ourFlag = null;
		private Flag theirFlag = null;
		
		const int SMODE_DEATHMATCH = 0;
		const int SMODE_GET_THEIR_FLAG = 1;
		
		private int currentMode = -1;
		
		private bool isDeathmatch = false;
		private Waypoint wpDeathmatch = null;
		private Stopwatch wpDmWatch = new Stopwatch();
		
		private static int snailBotIdx = 0;
		
		public static void Reset() {
			snailBotIdx = 0;
		}
		
		private static string[] snailBotNames = new string[]{
			"Not Fri",
			"Smurf",
			"Lil' Haste Jr.",
			"QelintiD",
			"Porculeitor",
			"GHOSTEK"
		};
		
		public static string[] botLines = new string[]{
			"chicky chicky", "Good public today", "u're good :(", "I hate pineapple pizza", "rage quit?",
			":p", ":)", "xD", ":D", "wtf?", "omg!", "lel", "i love this game", "call an ambulance lol!",
			"i hate this game", "join our discord!", "i'll get u next time buddy", "are you on discord?",
			"how many fake accounts does nino have?", "less talking, more killing", "kek.. nice shot",
			"this is so freaking op", "i feel useless", "this is nice", "this is fun", "so bad... just so bad",
			"NICE!", "what are you doing?", "this is so much better than s1", "kek", "when is MM back?",
			"hahha", "oh no :(", "NOOOO!", "fck this", "o_O", "aaaaaaah!", "????", "come at me bro!",
			"i am so bad" , "i am sooo good", "best game ever", "worst game ever", "runmode is for losers!",
			"am I warping?", "wazzup?", "lmfao", "eat that!", "bro?", "hmm?", "THD has to work on S2!",
			"need a new mouse", "need a new pc", "people still play s1?", "yesterday I went into a bar and talked to this chick...",
			"why are ppl still playing s1?", "what is this?", "what was that?", "why did dbfs leave the game?",
			"never seen such a good move", "never seen such a bad move", "...", "where's andy21? or is it andy69 lol?",
			"close one!", "is kinda cool game", "you should play ranked", "come ranked", "flak this shiiiiat",
			"+3", "+2", "+5", "u mad now?", "u mad?", "i'm starting to get tilted", "soldat3d when?",
			"-.-", "aayyy!", "let's go", "let's do this!", "come on!", "i just drank an energy drink", "don't talk like that",
			"i just ate a pizza", "i ordered a pizza", "let's hope my kid doesn't wake up", "y'all are big nobos",
			"good guy MM", "where is MM?", "s2 should be f2p", "s2 should have ingame shop", "I'm just smurfing...",
			"good devs", "what are the devs doing?", "it's hot", "i am a little bit tired", "ez", "too ez", "way too ez",
			"crap :(", "damn", "damn it!", "DAAAAAAAAMN", "no way in hell", "how?", "next! need new victims!",
			"omg how??!?!", "this weapon is so bad", "this weapon is so good", "op shit", "no competition here today :(",
			"nobody playing rankeds?", "you like my gostek?", "nice gostek!", "blubb", "my tema is sooo bad",
			"this needs to be fixed", "this needs to be changed", "argh!", "this guy is fast", "can you defend for once?",
			"this guy is slow", "grrrr", "nope", "just... nope", "what the fck?", "what the hell?", "go get the flag!",
			"kewl :)", "tststs", "never!", "always!", "sometimes!", "good for you", "u happy now?", "return the flag!",
			"10km chainsaw", "300ms ping", "laggers...", "kek... ghosteks", "well well!", "what the hell are you doing!",
			"what have we here?", "nice balance *kek*", "lovely.", "u did what?", "reported", "only champs can talk to me like that!",
			"probably have to go in 15min", "fix ranked when?", "new WM when?", "are you nino?", "shut up",
			"Soldat3 when?", "remind me to remind guerri about this", "nice try", "why?", "soldat is life",
			"my aim is gone", "can't aim shit right now", "oh boy", "oh no", "muahaha", "more s2 players when?",
			";)", "gotcha!", "got you!", "now u ded :)", "i eat noobs for breakfast", "guerri forgot his reminders today?",
			"road to bronze", "road to gold", "road to champ", "i'll be champ soon", "want to get gold III", "you're too slow dude..",
			"come back here!", "where do you think you're going?", "just... don't!", "ay caramba", "do you even know how to cannonball?",
			"one shots suck", "stop using op weapons", "nerf barret", "nerf mp5", "nerf m79", "nerf rheinmetall", "I saw horse on hippodrome yesterday",
			"nerf deagles", "nerf rl", "nerf steyr", "nerf kalashnikov", "buff minigun", "minigun kills matter", "so I tried LSD last week",
			"no more overheat", "unban delintiq when?", "good to know...", "just stop", "git gud kid", "git rekt kid", "I need to shower after this",
			"i'll rek you harder than last time", "this map is nice", "this map is good", "this map is so bad", "your movement is so bad...",
			"this map sucks", "i like this map", "i hate this map", "next map pliis", "who made this map?", "nice shot!",
			"whoever made this map is dumb", "whoever made this map is a genius", "hm?", "what u want from me?", "well done!",
			"this guy is too fast", "ranked is for noobs", "publics is for noobs", "recoil when?", "smoke nades when?", "that's what she said!",
			"new nades when?", "invisible nades again?", "nice lag", "cheater!", "hacker!", "nice hacks", "nice cheats", "urmom k?",
			"i'm actually fortnite pro!", "i'm actually valorant god", "i'm actually a pro CS:GO player", "i used to play soldat 1", "flak op?",
			"i used to play this game 15y ago", "i'm glad i bought this game", "WHAAAWAHWAHAWAHAHA", "uhuacuhrc..", "asdfghjlhlelheo", "nino op?",
			"*sigh*", "not again :(", "not this again", "shi..........T", "chatgpt when?", "new hats when?", "let's go other server?", "dD sux at running",
			"where are all the other players?", "i feel good today", "i feel sad today", "cardboard poop", "silver trash", "why did dbfs leave?",
			"you will all die!", "how dare you?", "xq's pepe memes are so stupid", "I like xq's pepe memes", "Where's horse?", "xq does too much memes... not enough soldat",
			"tbroo", "tbrooo!", "twawaw", "twaaaa", "tENGU!", "anybody got some movie recommendations?", "time for a break?", "MM.. kek",
			"you guys should take a break", "we will win this!", "we will lose :(", "you think you're better than me?", "proto new map when?",
			"is anna a girl?", "dang son", "damn son", "daaaaaang", "tdaang", "can you kill one guy?", "why? WHYYYYYY?", "NOOOOO!", "flak this", "flak that",
			"I hate paying taxes", "I'm 33... I feel so old now", "Did you know matrix came out in 1999?", "Any star wars fans here?", "oh yeh? oh YEH?",
			"dude....", "this is low quality...", "this is some high quality gameplay!", "You should stream?", "Anybody streaming?", "hey... what the fick?",
			"Ich mach dich fertig Junge!", "take that!", "people in the discord are weird", "people in the discord are kinda cool", "I don't like this Guerri",
			"I don't like this fri", "I don't like this Haste", "I don't like this Bee", "I don't like this proto", "I don't like this evh0", "boo! boo! boo!",
			"With your aim we're gonna lose", "With your movement we're gonna lose", "no wonder you're only bronze", "go back to bronze!", "xq is worst player ever",
			"lel... kek... gg", "gg wp", "gl hf", "need better tema", "my tema is so bad I wanna kill myself", "Tengu is a crybaby!",
			"if this game had one bug more it could win a nobel prize", "I'm a freaking genius", "I own many houses!", "did you know in spain you can occupy houses?",
			"I like making MM pepe art!", "Runmode is for true soldat playurs!", "Climbmode is where the action is!", "Why am I playing with you losers?",
			"Good job!", "Good job tema!", "I like my team. Good guys!", "xD", "no more overheat!", "flak kills matter!", "bruh?", "bruv?", "did you know nino is swede?",
			"did you know haste is a horse?", "did you know haste is a belgian horse?", "xQ probably busy designing houses.. he's an architect!", "giga kek",
			"your aim is nice", "your aim is bad", "my aim is so good I can snipe the wings off a fly!"
		};
		
		private static void GiveCoolAppearance(Player player) {
			player.props["skin color"] = new Color(1f,1f,1f).ToHex();
			player.props["hair color"] = new Color(1f,0f,1f).ToHex();
			player.controlled.GetComponent<GostekAppearance>().ForceRefresh();
		}
		
		private void FindFlags() {
			Flag ourFlag_ = null;
			Flag theirFlag_ = null;
		
			foreach(Flag flag in Flag._flags) {
				if(flag.team.Number == plrToControl.GetTeam())
					ourFlag_ = flag;
				else
					theirFlag_ = flag;
			}
			
			ourFlag = ourFlag_;
			theirFlag = theirFlag_;
		}
	
		public SnailBot(Player player, Flag ourFlag, Flag theirFlag) {
			
			this.plrToControl = player;

			//player.nick = snailBotNames[snailBotIdx % snailBotNames.Length];
			player.ping = 13;
			//snailBotIdx++;
			
			this.ourFlag = ourFlag;
			this.theirFlag = theirFlag;
			
			if(Map.GetLevelName().StartsWith("dm_"))
				isDeathmatch = true;
			
			outputNames[IDX_OUT_HAXS] = "HAXS";
			outputNames[IDX_OUT_JUMP] = "JUMP";
			outputNames[IDX_OUT_JETS] = "JETS";
			outputNames[IDX_OUT_SMAN] = "SMAN";
			outputNames[IDX_OUT_AIMX] = "AIMX";
			outputNames[IDX_OUT_AIMY] = "AIMY";
			outputNames[IDX_OUT_FIRE] = "FIRE";
			
			raycastVectors[IDX_RIGHT] = V_RIGHT;
			raycastVectors[IDX_LEFT] = V_LEFT;
			raycastVectors[IDX_UP] = V_UP;
			raycastVectors[IDX_DOWN] = V_DOWN;
			raycastVectors[IDX_DOWN_LEFT] = V_DOWN_LEFT;
			raycastVectors[IDX_DOWN_RIGHT] = V_DOWN_RIGHT;
			raycastVectors[IDX_UP_LEFT] = V_UP_LEFT;
			raycastVectors[IDX_UP_RIGHT] = V_UP_RIGHT;
			raycastVectors[IDX_UP_LEFT2] = V_UP_LEFT2;
			raycastVectors[IDX_UP_RIGHT2] = V_UP_RIGHT2;
			raycastVectors[IDX_DOWN_LEFT2] = V_DOWN_LEFT2;
			raycastVectors[IDX_DOWN_RIGHT2] = V_DOWN_RIGHT2;
			
			for(int i = 0; i < raycastVectors.Length; i++)
				raycastVectors[i] = raycastVectors[i].normalized;
			
			valueNames[IDX_RIGHT] = "RGHT";
			valueNames[IDX_LEFT] =  "LEFT";
			valueNames[IDX_UP] =    "UP__";
			valueNames[IDX_DOWN] =  "DOWN";
			valueNames[IDX_DOWN_RIGHT] = "DWRG";
			valueNames[IDX_UP_RIGHT] = "UPRG";
			valueNames[IDX_UP_LEFT] = "UPLF";
			valueNames[IDX_DOWN_LEFT] = "DWLF";
			
			valueNames[IDX_GOSTEK_JET] = "JET_";
			valueNames[IDX_GOSTEK_JUMP] = "JUMP";
			valueNames[IDX_GOSTEK_CAN_ROLL] = "CANR";
			valueNames[IDX_GOSTEK_JUMP_TIME] = "JMPT";
			valueNames[IDX_GOSTEK_GROUNDED] = "GRND";
			valueNames[IDX_GOSTEK_SUPERMAN] = "SMAN";
			
			valueNames[IDX_DOWN_LEFT2] = "DLF2";
			valueNames[IDX_DOWN_RIGHT2] = "DRG2";
			valueNames[IDX_UP_LEFT2] = "ULF2";
			valueNames[IDX_UP_RIGHT2] = "URG2";
			
			valueNames[IDX_GOSTEK_VELX] = "VEL_X";
			valueNames[IDX_GOSTEK_VELY] = "VEL_Y";
			
			valueNames[IDX_GOSTEK_SLOPE_LEFT] = "SLPL";
			valueNames[IDX_GOSTEK_SLOPE_RIGHT] = "SLPR";
		}
		
		private float RayCastDistance(Vector2 origin, Vector2 dir, float distance) {
			int hits = Physics2D.RaycastNonAlloc(origin, dir, raycastResults, distance, Map.Get.groundLayer);
			if(hits > 0)
				return raycastResults[0].distance;
			return distance+1;
		}
		
		private bool RayCastHit(Vector2 origin, Vector2 target) {
			Vector2 dir = target - origin;
			float distance = dir.magnitude;
			int hits = Physics2D.RaycastNonAlloc(origin, dir, raycastResults, distance, Map.Get.groundLayer);
			return hits > 0;
		}
		
		private Vector2? RayCastPoint(Vector2 origin, Vector2 dir) {
			int hits = Physics2D.RaycastNonAlloc(origin, dir, raycastResults, 500, Map.Get.groundLayer);
			if(hits > 0)
				return (Vector2)raycastResults[0].point;
			return null;
		}
		
		public void FixedUpdate() {
			if(plrToControl == null)
				return;
			
			if(plrToControl.controlled == null)
				return;
			
			if(plrToControl.controlled.dead)
				return;
			
			
			
			try {
				ScanEnvironment();
			} catch(Exception ex) {
				GameChat.ChatOrLog("// EXCEPTION DURING SCANENV. LINE REACHED := " + lineReached.ToString());
			}
			
			try {
				TakeControl();
			} catch(Exception ex) {
				GameChat.ChatOrLog("// EXCEPTION DURING TAKECTRL. LINE REACHED := " + lineReached.ToString());
				GameChat.ChatOrLog("//  Msg: " + ex.Message);
			}
		}
		
		private int doJumpFor = 0;
		private int goLeftFor = 0;
		private int stuckTimer = 0;
		private int goRightFor = 0;
		private int doJetFor = 0;
		private bool doCannonBall = false;
		private int waitSuperman = 0;
		private float killTargetDistance = 0f;
		private System.Random rand = new System.Random();
		
		
		private float RandomFloat() {
			double v = rand.NextDouble();
			if(rand.NextDouble() < 0.5)
				v *= -1;
			return (float)v;
		}
		
		private Vector2 RandomDirection() {
			return new Vector2(RandomFloat(), RandomFloat()).normalized;
		}
		
		bool didFire = false;
		
		
		private Vector2 altPos = new Vector2(0,0);
		private Vector2 lastPosition = new Vector2(0,0);
		
		private Queue<Waypoint> currentPath = null;
		private Waypoint currentWaypoint = null;
		private bool waitForJets = false;
		private Vector2 NULLVEC = new Vector2(0,0);
		
		public static int lineReached = -1;
	
		private void TakeControl() {
			
			Controls c = plrToControl.controlled;
			
			if(c == null)
				return;
			
			lineReached = 1;
			
			GostekMovement gm = c.GetComponent<GostekMovement>();
			
			if(gm.v.superman && gm.v.grounded) {
				//GameChat.ChatOrLog("SUPERMAN ON GROUND!!!");
				c.SetKey(Key.Jump, pressed: true);
				return; // leave superman if grounded :(
			} else if (gm.v.slide) {
				c.SetKey(Key.Superman, pressed: false);
				return; // don't slide :(
			}
			
			lineReached = 2;
			
			float hAxis = 0.0f;
			bool amIFlagger = false;
			Vector2 cpos = (Vector2)c.transform.position;
			Vector2 gotoPosition = NULLVEC;
			
			if(!isDeathmatch) {
				if(theirFlag == null || ourFlag == null) {
					FindFlags();
					return; // find flags first then
				}
				
				gotoPosition = (Vector2)theirFlag.transform.position;
				
				if(theirFlag.grabbed) {
					Player holder = theirFlag.lastTouchedPlayer;
					if(holder == plrToControl) {
						// Then go to our base?
						if((cpos - (Vector2)theirFlag.transform.position).magnitude < 10) {
							gotoPosition = (Vector2)ourFlag.basePoint;
							amIFlagger = true;
						}
					}
				}
				
				if(ourFlag.grabbed || !ourFlag.inbase) {
					if(!amIFlagger)
						gotoPosition = (Vector2)ourFlag.transform.position; // always chase EFC :D except when we are flagger
				}
				
				gotoPosition.y += 1;
			} else {
				if(wpDeathmatch != null)
					gotoPosition = wpDeathmatch.point;
			}
			
			lineReached = 3;
			
			
			bool canSeeKillTarget = false;
			
			if(gotoPosition.x < cpos.x) {
				hAxis = -1.0f;
			} else {
				hAxis = 1.0f;
			}
			
			lineReached = 101;
			
			
			if(RayCastHit(cpos, gotoPosition)) {
				// Ok so we can't see our goto position?
				// but can we see some waypoint or whatever?
				
				lineReached = 102;
				
				if(Math.Abs((double)cpos.x - (double)gotoPosition.x) <= 4) {
					if (goLeftFor <= 0 && goRightFor <= 0) {
						if(rand.NextDouble() < 0.5) {
							goLeftFor = 90;
							doJumpFor = 10;
						} else {
							goRightFor = 90;
							doJumpFor = 10;
						}
					}
					if(amIFlagger && stuckTimer > 120) {
						c.SetKey(Key.DropFlag, pressed: true);
					}
					stuckTimer++;
				} else {
					stuckTimer = 0;
					lineReached = 500;
					Waypoint wp = Waypoint.FindVisibleWaypoint(cpos, gotoPosition, hAxis);
					if(wp != null)
						gotoPosition = wp.point;
					lineReached = 100;
				}
			}
			
			lineReached = 4;

			
			if(plrToKill != null) {
				if(plrToKill.controlled != null) {
					Controls k = plrToKill.controlled;
					
					GostekMovement gm2 = k.GetComponent<GostekMovement>();
					

					Vector2 overAim = new Vector2(1, 2).normalized;
					
					
					Vector2 kpos = (Vector2)k.transform.position;

					
					if (kpos.x < cpos.x) {
						overAim.x *= -1;
					} else {
					}
					
					Vector2 toKillTarget = kpos - cpos;
					
					// Adjust for target velocity?
					int adjustmentFactor = 8 + rand.Next(4);
					int f = (int)(toKillTarget.magnitude / 6.0f);
					if(f <= 0)
						f = 1;
					adjustmentFactor /= f;
					if(adjustmentFactor <= 0)
						adjustmentFactor = 1;
					
					kpos += gm2.v.velocity / adjustmentFactor;
					
					lineReached = 5;
					
					float overAimFactor = toKillTarget.magnitude / 12.0f;
					
					killTargetDistance = RayCastDistance(cpos, toKillTarget, 500f);
					if(killTargetDistance > 100)
						killTargetDistance = -1;
					
					if(amIFlagger)
						if(killTargetDistance > 15)
							killTargetDistance = -1; // only shoot close enemies if we are flagger
					
					lineReached = 6;
					
					if(killTargetDistance > toKillTarget.magnitude) {
						canSeeKillTarget = true;
						ControlsExtensions.SetAimWorld(c, kpos + overAim * overAimFactor);
						if(true) {
							GostekWeapon gw = c.GetComponent<GostekWeapon>();
							if(gw && gw.weapon)
								if(gw.weapon.IsReady())
									c.SetKey(Key.Fire1, pressed: true);
							didFire = true;
						}
						else 
							didFire = false;
					} else {
						c.SetKey(Key.Fire1, pressed: false);
					}
					
					lineReached = 7;
				}
			}
			
			lineReached = 8;
			
			if (waitForJets) {
				if (c.GetComponent<GostekJets>().jetsAmount <= 0.9 && values[IDX_GOSTEK_GROUNDED] >= 0.99) {
					return;
				}
				else {
					waitForJets = false;
				}
			}
			
			if(gotoPosition.x < cpos.x) {
				hAxis = -1.0f;
			} else {
				hAxis = 1.0f;
			}
			
			if(!canSeeKillTarget)
				c.SetAimWorld(cpos + new Vector2(hAxis, 0));
			
			lineReached = 9;
			
			/*
			if((cpos - kpos).magnitude <= 6)
				c.SetKey(Key.Fire1, pressed: true);*/
			
			int _idx_down_dir, _idx_dir, _idx_gostek_slope_dir, _idx_up_dir2;
			
			if(hAxis < 0.0f) {
				_idx_dir = IDX_LEFT;
				_idx_down_dir = IDX_DOWN_LEFT;
				_idx_gostek_slope_dir = IDX_GOSTEK_SLOPE_LEFT;
				_idx_up_dir2 = IDX_UP_LEFT2;
				
			} else {
				_idx_dir = IDX_RIGHT;
				_idx_down_dir = IDX_DOWN_RIGHT;
				_idx_gostek_slope_dir = IDX_GOSTEK_SLOPE_RIGHT;
				_idx_up_dir2 = IDX_UP_RIGHT2;
			}
			
			lineReached = 10;
			
			// But can we move to the left?
			if (values[_idx_down_dir] > 0.01)
				c.SetAxis(Axis.H, hAxis);
			
			if (goLeftFor > 0) {
				goLeftFor--;
				c.SetAxis(Axis.H, -1.5f); // cheat :D
			} else if (goRightFor > 0) {
				goRightFor--;
				c.SetAxis(Axis.H, 1.5f); // also cheat
			}
			
			if(doJumpFor > 0) {
				c.SetKey(Key.Jump, pressed: true);
				doJumpFor--;
			} else {
				if(doCannonBall) {
					c.SetKey(Key.Jets, pressed: true);
					doJetFor = 2;
					if(values[_idx_gostek_slope_dir] <= 0.01)
						doJetFor = 45;
					doCannonBall = false;
					waitSuperman = 5;
				} else if(doJetFor > 0) {
					c.SetKey(Key.Jets, pressed: true);
					doJetFor--;
				} else {
					if (values[_idx_gostek_slope_dir] > 0.02 && values[IDX_GOSTEK_GROUNDED] <= 0.1) {
						if(values[IDX_GOSTEK_SUPERMAN] <= 0.1 && values[IDX_DOWN] > 0.05) {
							if(waitSuperman == 0)
								c.SetKey(Key.Superman, pressed: true);
							else
								waitSuperman--;
						}
						if(values[IDX_GOSTEK_SUPERMAN] >= 0.99) {
							c.SetKey(Key.Jets, pressed: true);
						}
					}
					
					if(values[IDX_GOSTEK_SUPERMAN] >= 0.99 && values[IDX_DOWN] <= 0.05) {
						c.SetKey(Key.Superman, pressed: true); 
					}
					
					if (values[_idx_gostek_slope_dir] < 0.01 && values[IDX_GOSTEK_GROUNDED] >= 0.99) {
						doJumpFor = 2;
						c.SetKey(Key.Jump, pressed: true);
					}
				}
			}
			
			lineReached = 11;
			
			if (values[IDX_GOSTEK_GROUNDED] >= 0.99 && Math.Abs(values[IDX_GOSTEK_VELX ]) > 0.2 && values[_idx_gostek_slope_dir] >= 0.005 && values[_idx_up_dir2] >= 0.05) {
				c.SetKey(Key.Jump, pressed: true);
				doJumpFor = 2;
				doCannonBall = true;
			}
			
			if (values[IDX_DOWN] >= 0.99 && values[IDX_GOSTEK_VELY] <= 0.12)
				c.SetKey(Key.Jets, pressed: true);
			
			if(doJumpFor == 0) {
				if (Math.Abs((double)cpos.x - (double)gotoPosition.x) <= 10) {
					// Ok, so we are under your gotoPosition?
					if (cpos.y < gotoPosition.y) {
						if (Math.Abs((double)cpos.x - (double)gotoPosition.x) <= 6)
							c.SetAxis(Axis.H, 0.0f); // stop then I guess
						
						if(c.GetComponent<GostekJets>().jetsAmount < 0.2f) {
							waitForJets = true;
						}
						
						if (values[IDX_GOSTEK_SUPERMAN] >= 0.99) {
							c.SetKey(Key.Superman, pressed: true); // exit superman
						} else {
							c.SetKey(Key.Superman, pressed: false);
						}
						
						if (values[IDX_GOSTEK_GROUNDED] >= 0.99) {
							c.SetKey(Key.Jump, pressed: true);
							doJumpFor = 10;
							doCannonBall = true;
						} else {
							c.SetKey(Key.Jets, pressed: true);
						}
					}
				}
			}
			
			lineReached = 12;
			
			if (values[IDX_GOSTEK_GROUNDED] >= 0.99 && values[IDX_GOSTEK_SUPERMAN] >= 0.99) {
				c.SetKey(Key.Superman, pressed: true); // leave superman if on ground.
			}
			
			lineReached = 13;
		}
		
		private void ScanEnvironment() {
			
			lineReached = -1;
			
			plrToKill = null;
			
			Controls targetControls = plrToControl.controlled;
			GostekMovement gm = targetControls.GetComponent<GostekMovement>();
			Vector2 origin = (Vector2)targetControls.transform.position;
			
			lineReached = -177;
			
			foreach(Player player in Players.Get.GetAlive()) {
				if(!player)
					continue;
				
				if(!player.controlled)
					continue;
				
				if(player.GetTeam() == plrToControl.GetTeam())
					if(!isDeathmatch)
						continue;
					
				lineReached = -109;
				
				// But can we see this player?
				if(RayCastHit(origin, (Vector2)player.controlled.transform.position))
					continue;
				
				plrToKill = player;
				if(rand.NextDouble() < 0.75)
					continue;
				break;
			}
			
			lineReached = -403;
			
			if(isDeathmatch) {
				if(wpDeathmatch == null) {
					lineReached = -408;
					wpDeathmatch = Waypoint.GetRandom();
					lineReached = -409;
					wpDmWatch.Reset();
					wpDmWatch.Start();
					lineReached = -455;
				} else {
					lineReached = -410;
					if(wpDmWatch.ElapsedMilliseconds >= 3000) {
						wpDmWatch.Stop();
						wpDmWatch.Reset();
						wpDmWatch.Start();
						wpDeathmatch = Waypoint.GetRandom();
					}
					lineReached = -444;
				}
			}
			
			lineReached = -999;
			
			for(int i = 0; i < raycastVectors.Length; i++) {
				float distance = RayCastDistance(origin, raycastVectors[i], 50f);
				distance /= 50f; // scale it to 1..0
				values[i] = (float)Math.Round((double)distance, 4);
			}
			
			if(gm.v.jet)
				values[IDX_GOSTEK_JET] = 1.0f;
			else
				values[IDX_GOSTEK_JET] = 0.0f;
			
			if(gm.v.jump)
				values[IDX_GOSTEK_JUMP] = 1.0f;
			else
				values[IDX_GOSTEK_JUMP] = 0.0f;
			
			if(gm.v.canRoll)
				values[IDX_GOSTEK_CAN_ROLL] = 1.0f;
			else
				values[IDX_GOSTEK_CAN_ROLL] = 0.0f;
			
			
			if(gm.v.jump && wasJumping)
				values[IDX_GOSTEK_JUMP_TIME] = (float)Math.Round((double)sw.ElapsedMilliseconds/(2000.0),4);
			else if (gm.v.jump && !wasJumping) {
				sw.Reset();
				sw.Start();
				wasJumping = true;
				values[IDX_GOSTEK_JUMP_TIME] = 0.0f;
			} else if (!gm.v.jump && wasJumping) {
				sw.Stop();
				wasJumping = false;
			}
			
			if(!gm.v.jump)
				values[IDX_GOSTEK_JUMP_TIME] = -1.0f;
			
			if(gm.v.grounded)
				values[IDX_GOSTEK_GROUNDED] = 1.0f;
			else
				values[IDX_GOSTEK_GROUNDED] = 0.0f;
			
			if(targetControls.IsPressed(Key.Jump))
				outputs[IDX_OUT_JUMP] = 1.0f;
			else
				outputs[IDX_OUT_JUMP] = 0.0f;
			
			if(targetControls.IsPressed(Key.Jets))
				outputs[IDX_OUT_JETS] = 1.0f;
			else
				outputs[IDX_OUT_JETS] = 0.0f;
			
			if(targetControls.IsPressed(Key.Superman))
				outputs[IDX_OUT_SMAN] = 1.0f;
			else
				outputs[IDX_OUT_SMAN] = 0.0f;
			
			if(targetControls.IsPressed(Key.Fire1))
				outputs[IDX_OUT_FIRE] = 1.0f;
			else
				outputs[IDX_OUT_FIRE] = 0.0f;
			
			if(gm.v.superman)
				values[IDX_GOSTEK_SUPERMAN] = 1.0f;
			else 
				values[IDX_GOSTEK_SUPERMAN] = 0.0f;
			
			outputs[IDX_OUT_HAXS] = targetControls.GetAxis(Axis.H);
			
			values[IDX_GOSTEK_VELX] = (float)Math.Round((double)(gm.v.velocity.x / 50f), 3);
			values[IDX_GOSTEK_VELY] = (float)Math.Round((double)(gm.v.velocity.y / 50f), 3);
			
			values[IDX_GOSTEK_SLOPE_LEFT] = values[IDX_DOWN_LEFT2] - values[IDX_DOWN_LEFT];
			values[IDX_GOSTEK_SLOPE_RIGHT] = values[IDX_DOWN_RIGHT2] - values[IDX_DOWN_RIGHT];

			Vector2 aimWorld = ControlsExtensions.GetAimWorld(targetControls) - origin;
			outputs[IDX_OUT_AIMX] = (float)Math.Round((double)(aimWorld.x / 50f), 4);
			outputs[IDX_OUT_AIMY] = (float)Math.Round((double)(aimWorld.y / 50f), 4);
		}
	}
	
	
	

	
	

	
	private void Awake() {
		if(!Net.IsServer) {
			Eventor.RemoveListener(Events.Player_Joined, OnPlayerJoinedLocal);
			Eventor.AddListener(Events.Player_Joined, OnPlayerJoinedLocal);
			Waypoint.HideWaypoints();
			
			/*foreach(Player bot in Players.Get.GetBots()) {
				if(bot.props.Exists("x-nick")) {
					bot.nick = (string)bot.props["x-nick"];
				}
			}*/
			
			return;
		}

		/*for(int i = 0; i < Global.botNames.Length; i++) {
			Global.botNames[i] = snailBotNames[i % snailBotNames.Length];
		}*/
		
		SnailBot.Reset();
		
		Time.fixedDeltaTime = 1.0f / 60.0f;
		
		GameChat.instance.OnChat.RemoveListener(this.OnChat);
		Eventor.RemoveListener(Events.Player_Joined, OnPlayerJoined);
        Eventor.RemoveListener(Events.Player_Left, OnPlayerLeft);
		Eventor.RemoveListener(Events.Died, OnDied);
		
		
		Waypoint.InitializePathFinding();
		
		Initialize();
		
		GameChat.ChatOrLog("INITIALIZED!");
		
		
		GameChat.instance.OnChat.AddListener(this.OnChat);
		Eventor.AddListener(Events.Player_Joined, OnPlayerJoined);
        Eventor.AddListener(Events.Player_Left, OnPlayerLeft);
		Eventor.AddListener(Events.Died, OnDied);
	}
	
	private void ShowFlags() {
		foreach(Flag flag in Flag._flags) {
			GameChat.ChatOrLog("#" + flag.team.Number.ToString() + " at: " + flag.transform.position.ToString() + " base: " + flag.basePoint.ToString());
		}
	}
	
	private void OnDestroy() {
		GameChat.instance.OnChat.RemoveListener(this.OnChat);
		Eventor.RemoveListener(Events.Player_Joined, OnPlayerJoined);
        Eventor.RemoveListener(Events.Player_Left, OnPlayerLeft);
		Eventor.RemoveListener(Events.Died, OnDied);
		Eventor.RemoveListener(Events.Player_Joined, OnPlayerJoinedLocal);
	}
	
	private System.Random rand = new System.Random();

	private void RandomBotChatter(string msg) {
		if(!Net.IsServer)
			return;

		SnailBot rsb = snailBots[rand.Next(snailBots.Count)];
		GameChat.instance.ServerChat("<color=#FEFEFE>[" + rsb.plrToControl.nick + "] " + msg + "</color>");
	}
	
	private void OnDied(IGameEvent ev) {
		if(snailBots.Count < 1)
			return;

		if (rand.NextDouble() < 0.03) {
			// Pick a random bot I guess?
			RandomBotChatter(SnailBot.botLines[rand.Next(SnailBot.botLines.Length)]);
		}
	}
	
	private void OnPlayerJoinedLocal(IGameEvent ev) {
		GlobalPlayerEvent gpe = ev as GlobalPlayerEvent;
		
		if(!gpe.Player.IsBot())
			return; // don't care about humans :)
		
		/*if(gpe.Player.props.Exists("x-nick"))
			gpe.Player.nick = (string)gpe.Player.props["x-nick"];*/
	}
	
	private void OnPlayerJoined(IGameEvent ev) {
		GlobalPlayerEvent gpe = ev as GlobalPlayerEvent;
		
		if(!gpe.Player.IsBot()) {
			return; // don't care about humans :)
		} /*else {
			foreach(Player p in Players.Get.GetHumans()) {
				if(Net.IsServer) {
					Players.Master_SyncAllPlayers(gpe.Player.id);
				}
			}
		}*/

		if(gpe.Player.nick.Contains("Honeybee")) {
			RandomBotChatter("Omg... a bee!");
		}
		else if(gpe.Player.nick.Contains("Haste")) {
			RandomBotChatter("Horse, is that you?");
		}
		
		AddSnailBotForPlayer(gpe.Player);
		//GameChat.ChatOrLog("BOTS NOW: " + snailBots.Count.ToString());
	}
	
	private void OnPlayerLeft(IGameEvent ev) {
		GlobalPlayerEvent gpe = ev as GlobalPlayerEvent;
		
		if(!gpe.Player.IsBot())
			return; // again we don't care about humans :)
		
		RemoveSnailBotForPlayer(gpe.Player);
		//GameChat.ChatOrLog("BOTS NOW: " + snailBots.Count.ToString());
	}
	
	private void OnChat(Player p, string msg) {
		if(msg == "!position") {
			Vector2 pos = Players.Get.GetHumans()[0].controlled.transform.position;
			GameChat.ChatOrLog("Your position is " + pos.ToString() + " " + (Map.Get.scale.x*Map.Get.width).ToString() + "," + Map.Get.height.ToString());
		}
	}
	
	
	/*
	private void OnGUI() {
		GUI.skin.label.fontSize = 22;
		GUI.skin.label.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
		
		for(int i = 0; i < values.Length; i++) {
			GUI.Label(new Rect(400, 50+25*i, 300, 100), valueNames[i] + "   " + values[i].ToString());
		}
		
		for(int i = 0; i < outputs.Length; i++) {
			GUI.Label(new Rect(200, 50+25*i, 300, 100), outputNames[i] + "    " + outputs[i].ToString());
		}
		
		GUI.Label(new Rect(600, 50, 300, 100), killTargetDistance.ToString());
	}
	*/
	
	
	
	
	
	long tick = 1;
	bool stopped = true;
	List<SnailBot> snailBots = new List<SnailBot>();
	
	private void RemoveSnailBotForPlayer(Player player) {
		SnailBot found = null;
		foreach(SnailBot snailBot in snailBots) {
			if (snailBot.plrToControl == player)
				found = snailBot;
		}
		snailBots.Remove(found);
	}
	
	private void AddSnailBotForPlayer(Player player) {
		Flag hisFlag = null;
		Flag theirFlag = null;
		
		foreach(Flag flag in Flag._flags) {
			if(flag.team.Number == player.GetTeam())
				hisFlag = flag;
			else
				theirFlag = flag;
		}
		
		snailBots.Add(new SnailBot(player, hisFlag, theirFlag));
	}
	
	private void Initialize() {
		snailBots.Clear();
		foreach(Player player in Players.Get.GetBots()) {
			AddSnailBotForPlayer(player);
		}
		stopped = false;
	}
	
	private void FixedUpdate() {
		
		if(!Net.IsServer)
			return;
		
		if(stopped)
			return;
		
		foreach(SnailBot snailBot in snailBots)
			snailBot.FixedUpdate();
	}
	
	
	
}
