# ATC dataserver

Project for the hands-on seminar.
Implements a dataserver providing a current air picture.

## Table of Contents

- [Introduction](#atc-dataserver)
- [API Documentation](#api-documentation)
  - [Endpoints](#endpoints)
- [Demo API](#demo-api)
- [Getting Started](#getting-started)
  - [Prerequisites](#prerequisites)
  - [Installation](#installation)



## API Documentation

The API for the project is simple. It exists of one endpoint, which responses includes all currently observed aircraft that are currently in a valid state.

### Endpoints

- **Endpoint 1**
  - Method: GET
  - URL: `/recognizedAirPicture/hso`

  - Response:
    ```
    [
    {
      "ID": "Transponder ID as string",
      "Estimates": Integer estimation,
      "Latitude": "Latitude as string",
      "Longitude": "Longitude as string",
      "PositionCreated": "Timestamp when position was received or estimated format "dd.mm.yyyy hours:minutes:seconds", example: 11.01.2024 10:54:0",
      "Track": Integer value between 0 and 359,
      "Callsign": "Callsign as string"
      },
      {
      "ID": "Transponder ID as string",
      "Estimates": Integer estimation,
      "Latitude": "Latitude as string",
      "Longitude": "Longitude as string",
      "PositionCreated": "Timestamp when position was received or estimated format "dd.mm.yyyy hours:minutes:seconds", example: 11.01.2024 10:54:0",
      "Track": Integer value between 0 and 359,
      "Callsign": "Callsign as string"
      }
    ]
    ```
  - Example Response:
      ```
      [
      	{
      		"ID": "3C0AC7",
      		"Estimates": 0,
      		"Latitude": "50,211028870830255",
      		"Longitude": "7,6721833764917005",
      		"PositionCreated": "11.01.2024 10:54:09",
      		"Track": 253,
      		"Callsign": "CFG3HD  "
      	},
      	{
      		"ID": "300781",
      		"Estimates": 0,
      		"Latitude": "47,03142",
      		"Longitude": "7,13425",
      		"PositionCreated": "11.01.2024 10:54:09",
      		"Track": 230,
      		"Callsign": "DLA06T  "
       	},
      ]
      ```

## Demo API

Included in this repository is a demo. The demo is build with Python and is implementing a flask server. The demo displays a folium map with markers representing the aircrafts.
The demo provides two API endpoints. One continuously updates the displayed map by creating a new Iframe and is loading the new map inside of it, the other creates said single map.

- **Endpoint 1**
  - Method: GET
  - URL: `/map`

  - Response:
      An index.html which is the base for the demo application

- **Endpoint 2**
  - Method: GET
  - URL: `/calculate_new_map`

  - Response:
      A map.html which contains the folium map. Is requested and loaded in an Iframe when opening index.html

## Getting Started
  Manual for using the application locally.

### Prerequisites
  - Docker
  - AWS SAM cli
  - AWS profile named "atc" with us-west-2 as selected region and arbitrary random access-keys
  - Access to the internal network of the HSO with the correct permissions
  - Several python libraries
    - folium
    - flask
    - tempfile
    - Pillow
For more information refer to the [imports for the demo](#Demo-imports)
   ### Demo imports
    from flask import Flask, render_template
    from PIL import Image
    import folium
    import tempfile
    import requests
    import json
    import io
    import os

### Installation
  ### Create Aws profile atc
  The following command will create an aws profile for your aws cli.
  Run ```aws configure --profile atc``` 
  For running the project locally, actual credentials do not need to be provided.

  ### Starting and initializing DynamoDB
  To start a DynamoDB Docker container, you can use the [docker-compose.yml](ATCDataserver/DynamoDebugSetUp/docker-compose.yml) file in the [DynamoDebugSetUp project](ATCDataserver/DynamoDebugSetUp).
  After that, running the [DynamoDebugSetUp project](ATCDataserver/DynamoDebugSetUp)will create a table RecognizedAirPicture for the DynamoDB
  ### Build a Serverless Application Model
  Navigate to [ATCDataserver](ATCDataserver) in a terminal. There the [template.yaml](ATCDataserver/template.yaml) file is located. Change the environment variable DYNAMODB_ENDPOINT_DEBUG to match your ip address. 
  > **Important Note:** Using the loopback address 127.0.0.1 or localhost will most likely not work
  ```
    Environment:
      Variables:
        DYNAMODB_ENDPOINT_DEBUG "http://<your_ip>:8000"
  ```
  Run the following command ```sam build```.
  This will create a build in the directory .aws-sam
  ### Start local API
  Run the command ```sam local start-api``` from the same [directory](ATCDataserver) as earlier. The command will run the lambda function locally, which can then be invoked with the url /recognizedAirPicture/hso
  as described in [Api Documentation](#API-Documentation) in a docker container at port 3000. Each invokation will trigger the creation of a new Docker container running the actual lambda function.
  ### Starting the ATCDataserver
  Connect to the internal network of HSO.
  Run the project [ATCDataserver](ATCDataserver/ATCDataserver).
  ### Starting the demo
  To start the demo, simply run [python_demo.py](Demo/python_demo.py). The demo will run on the localhost at port 5000.
  The endpoint `/calculate_new_map`, when accessed with a browser, will display the current air picture. Using the endpoint `/map` instead will continuously call `/calculate_new_map` and update the map on display every few seconds.
  For continuous updates on the map use the endpoint `/map`



