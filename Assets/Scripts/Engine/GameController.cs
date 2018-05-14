﻿using UnityEngine;
using System.Collections;
using Newtonsoft.Json;
using System.IO;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using Assets.Scripts;
using System.Collections.Generic;

public class GameController : MonoBehaviour
{
    private static List<GameObject> players;
    public GameObject PlayerPrefab;
    private static int currentPlayerIndex;

    public GameObject PlanetPrefab;
    public GameObject StartPrefab;
    public GameObject ScoutPrefab;
    public GameObject ColonizerPrefab;

    private static int year;

    private HexGrid grid;

    // Use this for initialization
    void Start()
    {
        StartCoroutine(DelayedStart());
    }

    IEnumerator DelayedStart()
    {
        grid = GameObject.Find("HexGrid").GetComponent<HexGrid>();

        InitPlayers();
        InitMap();
        yield return new WaitForSeconds(0.1f);  // spaceships after hex grid
        InitSpaceships();

        yield return new WaitForSeconds(0.25f); // start after all the rest
        StartGame();
    }

    void InitPlayers()
    {
        // Create players from prefab.
        players = new List<GameObject>();
        players.Add(Instantiate(PlayerPrefab));
        players[0].GetComponent<Player>().human = true;
        players[0].name = "Player";

        for (int i = 1; i <= 1; i++)
        {
            players.Add(Instantiate(PlayerPrefab));
            players[i].GetComponent<Player>().human = false;
            players[i].name = "AI-" + i;
        }

        currentPlayerIndex = 0;
    }

    void InitSpaceships()
    {
        GameObject spaceship;
        foreach (GameObject player in players)
        {
            Planet homePlanet = player.GetComponent<Player>().GetPlanets().Cast<Planet>().First();

            // 1x scout
            for (int i = 0; i < 1; i++)
            {
                spaceship = SpaceshipFromPref(ScoutPrefab, homePlanet);
                spaceship.GetComponent<Spaceship>().Init();
                spaceship.GetComponent<Spaceship>().Owned(player.GetComponent<Player>());
            }

            // 0x colonizer
            for (int i = 0; i < 0; i++)
            {
                spaceship = SpaceshipFromPref(ColonizerPrefab, homePlanet);
                spaceship.GetComponent<Spaceship>().Init();
                spaceship.GetComponent<Spaceship>().Owned(player.GetComponent<Player>());
            }
        }
    }

    HexCell EmptyCell(HexCoordinates startCooridantes)
    {
        // serch for empty hexCell
        HexCell cell;
        for (int X = -1; X <= 1; X += 2)
        {
            HexCoordinates newCoordinates = new HexCoordinates(startCooridantes.X + X, startCooridantes.Z);
            cell = grid.FromCoordinates(newCoordinates);
            if (cell != null && cell.IsEmpty())
                return cell;
        }
        for (int Z = -1; Z <= 1; Z += 2)
        {
            HexCoordinates newCoordinates = new HexCoordinates(startCooridantes.X, startCooridantes.Z + Z);
            cell = grid.FromCoordinates(newCoordinates);
            if (cell != null && cell.IsEmpty())
                return cell;
        }
        return null;
    }

    GameObject SpaceshipFromPref(GameObject spaceshipPrefab, Planet startPlanet)
    {

        HexCoordinates homePlanetCoordinates = HexCoordinates.FromPosition(startPlanet.transform.position);
        HexCell spaceshipGrid = EmptyCell(homePlanetCoordinates);

        if (spaceshipGrid != null)
        {
            return Instantiate(spaceshipPrefab, spaceshipGrid.transform.position, Quaternion.identity);//.GetComponent<Spaceship>();
        }
        else
        {
            Debug.Log("Can't find empty cell for spaceship " + spaceshipPrefab.name + " for planet " + startPlanet.name);
        }
        return null;
    }

    void InitMap()
    {
        // Create map from file / random.
        // todo: in main menu we should decide if map is from file or random and set parameters
        // todo: move json deserialization to Planet's FromJson method
        // serializacje w unity ssie, trzeba bedzie doprawcowac (potrzebne bedzie do save/load i pewnie networkingu...)
        // todo: w jsonach nie moze byc utf8

        JObject o = JObject.Parse(Resources.Load("map1").ToString());
        InitPlanets((JArray)o["planets"]);
        InitStars((JArray)o["stars"]);
    }

    void InitPlanets(JArray jPlanetsCollection)
    {
        int playersWithHomePLanet = 0;

        foreach (JObject jPlanetSerialized in jPlanetsCollection)
        {
            GameObject planet = Instantiate(original: PlanetPrefab, position: new Vector3(
                (float)jPlanetSerialized["position"][0], (float)jPlanetSerialized["position"][1], (float)jPlanetSerialized["position"][2]), rotation: Quaternion.identity
            );
            JsonUtility.FromJsonOverwrite(jPlanetSerialized["planetMain"].ToString(), planet.GetComponent<Planet>());
            planet.name = jPlanetSerialized["name"].ToString();

            float radius = (float)jPlanetSerialized["radius"];
            //planet.GetComponent<SphereCollider>().radius = radius;
            planet.transform.localScale = new Vector3(radius, radius, radius);

            string materialString = (string)jPlanetSerialized["material"];
            if (materialString != null)
            {
                Material newMaterial = Resources.Load(materialString, typeof(Material)) as Material;
                if (materialString != null)
                    planet.GetComponentsInChildren<MeshRenderer>()[0].material = newMaterial;
            }

            if ((bool)jPlanetSerialized["mayBeHome"] == true && playersWithHomePLanet < players.Count())
            {
                planet.GetComponent<Planet>().Colonize(players[playersWithHomePLanet].GetComponent<Player>());
                playersWithHomePLanet++;
            }
        }

        if (playersWithHomePLanet < players.Count())
        {
            throw new Exception("Not enough planets for players");
        }
    }

    void InitStars(JArray jStarsCollection)
    {
        foreach (JObject jStarSerialized in jStarsCollection)
        {
            GameObject star = Instantiate(original: StartPrefab, position: new Vector3(
                (float)jStarSerialized["position"][0], (float)jStarSerialized["position"][1], (float)jStarSerialized["position"][2]), rotation: Quaternion.identity
            );
            star.name = jStarSerialized["name"].ToString();

            float radius = (float)jStarSerialized["radius"];
            star.GetComponent<SphereCollider>().radius = radius;
            star.transform.localScale = new Vector3(radius, radius, radius);

            string materialString = (string)jStarSerialized["material"];
            if (materialString != null)
            {
                Material newMaterial = Resources.Load(materialString, typeof(Material)) as Material;
                if (materialString != null)
                    star.GetComponentsInChildren<MeshRenderer>()[0].material = newMaterial;
            }
        }
    }

    void StartGame()
    {
        Debug.Log("Starting game");
        currentPlayerIndex = players.Count() - 1; // NextTurn will wrap index to zero at the beginning
        year = -1;  // NextTurn will increment Year at the beginning
        NextTurn();
    }

    public void NextTurn()
    {
        currentPlayerIndex = (currentPlayerIndex + 1) % players.Count();
        if (currentPlayerIndex == 0)
        {
            year++;
            Debug.Log("New year: " + year);
        }

        foreach (Ownable owned in GetCurrentPlayer().GetOwned())
        {
            owned.SetupNewTurn();
        }

        EventManager.selectionManager.SelectedObject = null;
        grid.SetupNewTurn(GetCurrentPlayer());
        GameObject.Find("MiniMap").GetComponent<MiniMapController>().SetupNewTurn(GetCurrentPlayer());

        Debug.Log("Next turn, player: " + GetCurrentPlayer().name);
    }

    public static Player GetCurrentPlayer()
    {
        return players[currentPlayerIndex].GetComponent<Player>();
    }

    public static int GetYear()
    {
        return year;
    }

    public void Colonize()
    {
        var colonizer = EventManager.selectionManager.SelectedObject.GetComponent<Colonizer>();
        if (colonizer != null)
        {
            if (colonizer.ColonizePlanet())
            {
                grid.FromCoordinates(colonizer.Coordinates).ClearObject();
                GetCurrentPlayer().Lose(colonizer);
                
                Destroy(colonizer.gameObject);

            }
        }

    }
    public void BuildSpaceship(GameObject spaceshipPrefab)
    {
        var planet = EventManager.selectionManager.SelectedObject.GetComponent<Planet>();
        if (planet != null)
        {
            if (planet.IsPossibleBuildSpaceship())
            {
                GameObject spaceship = planet.BuildSpaceship(spaceshipPrefab);
                spaceship.GetComponent<Spaceship>().Owned(GetCurrentPlayer());
                spaceship.GetComponent<Spaceship>().Init();

                EventManager.selectionManager.SelectedObject = null;
                grid.SetupNewTurn(GetCurrentPlayer());
                GameObject.Find("MiniMap").GetComponent<MiniMapController>().SetupNewTurn(GetCurrentPlayer());
            }
        }
    }

    // Update is called once per frame
    void Update()
    {

    }
}
