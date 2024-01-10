from PIL import Image, ImageDraw, ImageFont
import folium
import tempfile
import requests
import json
import io
import os
# plane marker from <a target="_blank" href="https://icons8.com/icon/o21PmwHBoj5l/flugmodus-an">Flugmodus an</a> Icon von <a target="_blank" href="https://icons8.com">Icons8</a>



# Get the directory of the current script
script_directory = os.path.dirname(os.path.abspath(__file__))

# Construct paths to files in subdirectories
static_folder = os.path.join(script_directory, 'static')
html_file_path = os.path.join(static_folder, 'map.html')

plane_image = os.path.join(script_directory, 'icons8-flugmodus-an-64.png')


# partly created with chatgpt
def create_folium_map(latitude, longitude, zoom=12):
    # Create a Folium map centered at the specified latitude and longitude
    map_object = folium.Map(location=[latitude, longitude], zoom_start=zoom)

    # Save the map to an HTML file
    map_object.save(html_file_path)

    return map_object


def rotate_image(image_path, rotation_angle_degrees):
    # Open the image
    original_image = Image.open(image_path)

    # Rotate the image, added the -(rotation_angle-degrees -90) to turn the right direction
    rotated_image = original_image.rotate(-(rotation_angle_degrees - 90), expand=True) 

    # Save the rotated image to a BytesIO buffer
    rotated_image_buffer = io.BytesIO()
    rotated_image.save(rotated_image_buffer, format="PNG")


    temp_file = tempfile.NamedTemporaryFile(delete=False, suffix=".png")
    rotated_image.save(temp_file.name, format="PNG")

    return temp_file.name


def draw_image_on_map(map_object, position, rotated_image_path, popup_text="Image Marker"):

    # Add a marker with a rotated image icon
    icon = folium.CustomIcon(
        rotated_image_path,
        icon_size=(30, 30),  # Adjust the size as needed
    )

    folium.Marker(
        location=position,
        popup=popup_text,
        icon=icon,
    ).add_to(map_object)

    # Save the map to an HTML file
    map_object.save(html_file_path)


def deserialize_air_picture(json_air_picture):
    
    air_picture = json.loads(json_air_picture)

    return air_picture


def get_air_picture():
    url = "http://localhost:3000/recognizedAirPicture/hso"
    response = requests.get(url)

    if response.status_code == 200:
        return response.text
    

if __name__ == "__main__":
    

    latitude = 48.457629
    longitude = 7.941779  
    map = create_folium_map(latitude=latitude, longitude=longitude, zoom=7)

    air_picture_json = get_air_picture()
    result = deserialize_air_picture(air_picture_json)
    count = 0
    for e in result:
        count = count + 1
        position = [float(e["Latitude"].replace(",", ".")), float(e["Longitude"].replace(",", "."))]
        rotated_image = rotate_image(plane_image, e["Track"])
        draw_image_on_map(map_object=map, position=position, rotated_image_path=rotated_image, popup_text=e)
    print("Counted aircraft: ", count)
    
