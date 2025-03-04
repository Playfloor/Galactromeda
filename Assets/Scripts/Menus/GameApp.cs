/*
    Copyright (c) 2018, Szymon Jakóbczyk, Paweł Płatek, Michał Mielus, Maciej Rajs, Minh Nhật Trịnh, Izabela Musztyfaga
    All rights reserved.

    Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

        * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
        * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation 
          and/or other materials provided with the distribution.
        * Neither the name of the [organization] nor the names of its contributors may be used to endorse or promote products derived from this software 
          without specific prior written permission.

    THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT 
    LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT 
    HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT 
    LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON 
    ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE 
    USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
using System;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking.NetworkSystem;

/*
 *  Class for global configuration and persistance data between scenes
 *  Singleton, created at "MainMenuScene", should be available all the time
 *  
 *  When some scene with input fields will be changed soon, this class can persists data from the scene
 */
public class GameApp : MonoBehaviour
{
    private static GameApp instance;
    private LevelLoader levelLoader;

    // base path for saved games files and new game files
    public string startMapsPath;
    public string savedGamesPath;

    // ids for network messaging
    public readonly short connMapJsonId = 1337;
    public readonly short connAssignPlayerId = 20001;
    public readonly short connAssignPlayerErrorId = 20002;
    public readonly short connAssignPlayerSuccessId = 20003;
    public readonly short connSetupTurnId = 20004;
    public readonly short connClientLoadGameId = 20005;
    public readonly short connClientEndGame = 20006;

    // all prefabs in one place
    public GameObject PlayerPrefab;
    public GameObject PlanetPrefab;
    public GameObject StartPrefab;
    public GameObject ScoutPrefab;
    public GameObject ColonizerPrefab;
    public GameObject MinerPrefab;
    public GameObject WarshipPrefab;

    public GameObject ExplosionPrefab;
    public GameObject AttackPrefab;
    public GameObject HitPrefab;
    public GameObject MinePrefab;

    public GameObject PlayerMenuPrefab;

    // variables that will be available between scenes
    public Dictionary<string, string> Parameters;
    List<PlayerMenu> playerMenuList;

    //public Stack<Color> colorStack = [new Color(1,0.1f,0.1f),new Color(0.1f,0.1f,1), new Color(1f, 1f, 0.1f)];

    // data that will be saved, based on scene name
    // Dictionary<scene name, List<{saved value name, path to input filed in editor}>>
    private Dictionary<string, List<ParameterMapping>> parametersToPersist = new Dictionary<string, List<ParameterMapping>>
    {
        {"GameScene",  new List<ParameterMapping> {
                new ParameterMapping { name="SavedGameFile", inputField = "MenuCanvas/SavedGameFileInput" }
            }
        },
        {"LoadGameMapScene",  new List<ParameterMapping> {
                new ParameterMapping { name="ServerAddress", inputField = "MenuCanvas/ServerAddressInput" },
                new ParameterMapping { name="ServerPort", inputField = "MenuCanvas/ServerPortInput" },
                new ParameterMapping { name="GameToLoad", inputField = "MenuCanvas/DropdownCanvas/GameToLoadDropdown" },
            }
        },
        {"JoinGameScene",  new List<ParameterMapping> {
                new ParameterMapping { name="ServerAddress", inputField = "MenuCanvas/ServerAddressInput" },
                new ParameterMapping { name="ServerPort", inputField = "MenuCanvas/ServerPortInput" },
                new ParameterMapping { name="PlayerName", inputField = "MenuCanvas/PlayerNameInput" },
                new ParameterMapping { name="Password", inputField = "MenuCanvas/PasswordInput" }
            }
        },
        {"NewGameMapScene",  new List<ParameterMapping> {
                new ParameterMapping { name="ServerAddress", inputField = "MenuCanvas/ServerAddressInput" },
                new ParameterMapping { name="ServerPort", inputField = "MenuCanvas/ServerPortInput" },
                new ParameterMapping { name="MapToLoad", inputField = "MenuCanvas/DropdownCanvas/MapToLoadDropdown" },
            }
        }
    };

    // this files should be in Assets/Configs/Resources/StartMaps
    private List<string> startMapsList = new List<string> {
        "1. Small- 2 Players", "2. Medium- 4 Players", "3. Large- 8 Players"
    };

    // Scripts can receive inputs by "name". Inputs are found by object name ("inputField") in editor 
    private struct ParameterMapping
    {
        public string name;
        public string inputField;
    }

    public struct PlayerMenu
    {
        public string name;
        public string password;
        public string race;
        public string playerType;
    }

    public struct TurnStatus
    {
        public int status;
        public string msg;
    }

    /*
     *  Read json file given path
     */
    public JObject ReadJsonFile(string path)
    {
        Debug.Log("GameApp ReadJsonFile: " + path);

        StreamReader reader = new StreamReader(path);
        string fileReaded = reader.ReadToEnd();
        reader.Close();

        if (fileReaded == null || "".Equals(fileReaded))
        {
            throw new Exception("fileReaded is null, path: " + path);
        }

        JObject fileParsed = JObject.Parse(fileReaded);
        if (fileParsed == null)
        {
            throw new Exception("Error loading json");
        }

        return fileParsed;
    }

    void Awake()
    {
        if (instance == null)
        {
            Debug.Log("Awake GameApp");

            var x = new StringMessage(new string('A', 500000));

            Parameters = new Dictionary<string, string>();
            playerMenuList = new List<PlayerMenu>();

            levelLoader = GameObject.Find("LevelLoader").GetComponent<LevelLoader>();

            // path to (on win10) <User>/AppData/LocalLow/Informatyka/Stars!/Configs/StartMaps
            startMapsPath = Application.persistentDataPath + "/Configs/StartMaps/";
            Debug.Log("GameApp startMapsPath: " + startMapsPath);

            // path to (on win10) <User>/AppData/LocalLow/Informatyka/Stars!/Configs/SavedGames
            savedGamesPath = Application.persistentDataPath + "/Configs/SavedGames/";
            Debug.Log("GameApp savedGamesPath: " + savedGamesPath);

            // move files from asset do persistent data path
            // path to Assets/Configs/Resources/StartMaps/, included in build
            string savedGamesAssetPath = "StartMaps/";

            if (!Directory.Exists(savedGamesPath))
                Directory.CreateDirectory(savedGamesPath);

            if (!Directory.Exists(startMapsPath))
                Directory.CreateDirectory(startMapsPath);

            foreach (string mapToSave in startMapsList)
            {
                TextAsset mapAsset = Resources.Load(savedGamesAssetPath + mapToSave) as TextAsset;
                if (mapAsset != null)
                {
                    string path = startMapsPath + "/" + mapToSave + ".json";
                    Debug.Log("Saving map from assert to file: " + path);

                    StreamWriter streamWriter = new StreamWriter(path);
                    streamWriter.Write(mapAsset.text);
                    streamWriter.Close();
                }
            }

            DontDestroyOnLoad(gameObject);
            instance = this;
        } else if(instance != this)
        {
            Destroy(gameObject);
        }
    }

    /*
     *  Persists players data from "NewGameScene". todo: change to list
     */
    public List<PlayerMenu> GetAllPlayersFromMenu()
    {
        return playerMenuList;
    }

    public void SavePlayersFromMenu(List<PlayerMenu> playerMenuList)
    {
        this.playerMenuList = playerMenuList;
    }


    // scene inputs persistance

    public void RemoveAllParameters()
    {
        Parameters.Clear();
    }

    public void RemoveAllParameters(string scene)
    {
        if (parametersToPersist.ContainsKey(scene))
        {
            foreach (ParameterMapping parameterMapping in parametersToPersist[scene])
            {
                Parameters.Remove(parameterMapping.name);
            }
        }
    }

    /*
     *  Get current scene name and persists all input fields
     */
    public void PersistAllParameters(string scene)
    {
        if (parametersToPersist.ContainsKey(scene))
        {
            foreach (ParameterMapping parameterMapping in parametersToPersist[scene])
            {
                PersistInputField(parameterMapping.name, parameterMapping.inputField);
            }
        }

        Debug.Log("PersistAllParameters done:");
        foreach (string param in Parameters.Keys)
        {
            Debug.Log(param + " - " + Parameters[param]);
        }
    }

    private string FindInputFiled(string inputFieldName)
    {
        if (GameObject.Find(inputFieldName) != null)
        {
            if (inputFieldName.Contains("Dropdown"))
            {
                Dropdown inputField = GameObject.Find(inputFieldName).GetComponent<Dropdown>();
                if (inputField != null && inputField.value < inputField.options.Count)
                {
                    // that .json cause dropdown are used only for files
                    return inputField.options[inputField.value].text + ".json";
                }
            }
            else
            {
                InputField inputField = GameObject.Find(inputFieldName).GetComponent<InputField>();
                if (inputField != null)
                {
                    return inputField.text;
                }
            }
        }
        return null;
    }

    public void PersistInputField(string key, string inputFieldName)
    {
        string value = FindInputFiled(inputFieldName);
        if (value != null)
        {
            if (Parameters.ContainsKey(key))
                Parameters.Remove(key);
            Parameters.Add(key, value);
        }
    }

    public void RemoveInputField(string key)
    {
        if (Parameters.ContainsKey(key))
            Parameters.Remove(key);
    }

    public string GetAndRemoveInputField(string key)
    {
        if (Parameters.ContainsKey(key))
        {
            string toReturn = Parameters[key];
            Parameters.Remove(key);
            return toReturn;
        }
        return null;
    }

    public string GetInputField(string key)
    {
        if (Parameters.ContainsKey(key))
        {
            return Parameters[key];
        }
        return null;
    }

}
