﻿using Assets.Scripts;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

public class Planet : MonoBehaviour
{ 
    private MyUIHoverListener uiListener;

    HexGrid grid;
    public HexCoordinates Coordinates { get; set; }

    // Use this for initialization
    void Start()
    {
        grid = (GameObject.Find("HexGrid").GetComponent("HexGrid") as HexGrid);

        uiListener = GameObject.Find("WiPCanvas").GetComponent<MyUIHoverListener>();

        UpdateCoordinates();
    }

    void UpdateCoordinates()
    {
        Coordinates = HexCoordinates.FromPosition(gameObject.transform.position);
        if (grid.FromCoordinates(Coordinates) != null) transform.position = grid.FromCoordinates(Coordinates).transform.localPosition; //Snap object to hex
        if (grid.FromCoordinates(Coordinates) != null) grid.FromCoordinates(Coordinates).AssignObject(this.gameObject);
        //Debug.Log(grid.FromCoordinates(Coordinates).transform.localPosition.ToString() + '\n' + Coordinates.ToString());
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnMouseUpAsButton()
    {
        if (!uiListener.isUIOverride) EventManager.selectionManager.SelectedObject = this.gameObject;
    }
}
