﻿using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.Networking.NetworkSystem;
using System;

/*
 *  This is singleton object
 *  Created at "JoinGameScene"
 *  Destroyed at game exit or "Back" button in "JoinGameScene" (in LevelLoader.Back)
 */
public class ClientNetworkManager : NetworkManager
{

    private GameController gameController;
    private GameApp gameApp;
    public NetworkClient networkClient;
    public NetworkConnection connection;

    private static bool created = false;

    void Awake()
    {
        if (!created)
        {
            gameApp = GameObject.Find("GameApp").GetComponent<GameApp>();

            DontDestroyOnLoad(this.gameObject);
            created = true;
            Debug.Log("Awake: " + this.gameObject);
        }
    }

    /*
     *   After "Join" button in "JoinGameScene"
     *   Setup server address and port, start client
     */
    public void SetupClient()
    {
        Debug.Log("SetupClient");

        try
        {
            // persists config data from menu scene
            gameApp.PersistAllParameters("JoinGameScene");

            this.networkAddress = gameApp.GetAndRemoveInputField("ServerAddress");
            this.networkPort = int.Parse(gameApp.GetAndRemoveInputField("ServerPort"));
        } catch(Exception e)
        {
            Debug.Log("SetupClient error: " + e.Message);
            gameApp.RemoveAllParameters();
            return;
        }

        this.networkAddress = "192.168.1.10";
        this.networkPort = 7777;
        this.StartClient();
    }

    // Client callbacks

    /*
     *  After connection to the server, scene should changed to "GameScene"
     *  GameController should be available
     */
    public override void OnClientSceneChanged(NetworkConnection conn)
    {
        Debug.Log("OnClientSceneChanged: " + conn);
        base.OnClientSceneChanged(conn);

        gameController = GameObject.Find("GameController").GetComponent<GameController>();
        gameController.clientNetworkManager = this;
    }

    /*
     *  Invoked when client connects to the server
     *  It sends message with player name (connAssignPlayerId), server should then send connAssignPlayerErrorId, connAssignPlayerSuccessId or connClientReadyId
     */
    public override void OnClientConnect(NetworkConnection conn)
    {
        //base.OnClientConnect(conn);
        Debug.Log("Connected successfully to server");

        NetworkServer.RegisterHandler(GameApp.connAssignPlayerErrorId, OnClientAssignPlayerError);
        NetworkServer.RegisterHandler(GameApp.connAssignPlayerSuccessId, OnClientAssignPlayerSuccess);
        NetworkServer.RegisterHandler(GameApp.connClientReadyId, OnClientReady);

        string playerName = gameApp.GetAndRemoveInputField("PlayerName");
        string password = gameApp.GetAndRemoveInputField("Password");

        StringMessage playerMsg = new StringMessage(playerName);
        networkClient.Send(GameApp.connAssignPlayerId, playerMsg);
    }


    /*
     *  Custom callback (on connAssignPlayerErrorId)
     *  Server invoke it when client can't join the game
     *  May be wrong player name, or the player is taken already
     */
    public void OnClientAssignPlayerError(NetworkMessage netMsg)
    {
        Debug.Log("OnClientAssignPlayerError: " + netMsg.ReadMessage<StringMessage>());
    }

    /*
     * Custom callback (on connAssignPlayerSuccessId)
     * Server invoke it when client joinned to the game
     * Client will wait for the turn
     */
    public void OnClientAssignPlayerSuccess(NetworkMessage netMsg)
    {
        Debug.Log("OnClientAssignPlayerSuccess: " + netMsg.ReadMessage<StringMessage>());
        gameController.WaitForTurn();
    }

    /*
     *  Custom callback (on connClientReadyId)
     *  Server invoke it when it is this client's turn
     *  Client set to "ready" state and start plays the game
     */
    public void OnClientReady(NetworkMessage netMsg)
    {
        Debug.Log("OnClientReady");
        ClientScene.Ready(netMsg.conn);
        gameController.StopWaitForTurn();
    }

    /*
     *  Called in GameController after next turn
     *  Client should wait
     */
    public override void OnClientNotReady(NetworkConnection conn)
    {
        Debug.Log("Server has set client to be not-ready (stop getting state updates): " + conn);
        gameController.WaitForTurn();
    }


    public override void OnClientDisconnect(NetworkConnection conn)
    {
        StopClient();
        if (conn.lastError != NetworkError.Ok)
        {
            if (LogFilter.logError) { Debug.LogError("ClientDisconnected due to error: " + conn.lastError); }
        }
        Debug.Log("Client disconnected from server: " + conn);
    }

    public override void OnClientError(NetworkConnection conn, int errorCode)
    {
        Debug.Log("Client network error occurred: " + (NetworkError)errorCode);
    }

    public override void OnStartClient(NetworkClient client)
    {
        Debug.Log("Client has started");
        networkClient = client;
    }

    public override void OnStopClient()
    {
        Debug.Log("Client has stopped");
    }

}