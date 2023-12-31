import folium
import math
from PIL import Image, ImageDraw, ImageFont
import io
import tempfile
import base64
# plane marker from <a target="_blank" href="https://icons8.com/icon/o21PmwHBoj5l/flugmodus-an">Flugmodus an</a> Icon von <a target="_blank" href="https://icons8.com">Icons8</a>
def create_folium_map(latitude, longitude, zoom=12):
    # Create a Folium map centered at the specified latitude and longitude
    map_object = folium.Map(location=[latitude, longitude], zoom_start=zoom)

    # Save the map to an HTML file
    map_object.save("map.html")

    return map_object

# Created with support from ChatGPT
def open_folium_map():

    # Open the HTML file in the default web browser
    import webbrowser
    webbrowser.open("map.html")

def degree_to_vector(degrees):
    
    radians = math.radians(degrees)
    return [math.cos(radians), math.sin(radians)]

def add_arrow_marker(map_object, position, direction_degrees, popup_text="Arrow"):
    # Convert direction to a vector
    direction_vector = degree_to_vector(direction_degrees)

    # Calculate the end position based on the direction and length
    end_position = [position[0] + direction_vector[0] * 0.01, position[1] + direction_vector[1] * 0.01]

    # Create a Folium map centered at the specified position
    map_object = folium.Map(location=position, zoom_start=12)

    # Add a polyline representing the arrow
    folium.PolyLine([position, end_position], color="blue", weight=2.5, opacity=1).add_to(map_object)

    # Add a marker at the end of the arrow
    folium.Marker(
        location=end_position,
        popup=popup_text,
        icon=folium.Icon(icon="arrow-up", prefix="fa", color="red"),
    ).add_to(map_object)
    map_object.save("map.html")


def rotate_image(image_path, rotation_angle_degrees):
    # Open the image
    original_image = Image.open(image_path)

    # Rotate the image
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
        rotated_image,
        icon_size=(30, 30),  # Adjust the size as needed
    )

    folium.Marker(
        location=position,
        popup=popup_text,
        icon=icon,
    ).add_to(map_object)

    # Save the map to an HTML file
    map_object.save("map.html")

if __name__ == "__main__":


    # coordinates hsog
    latitude = 48.457629
    longitude = 7.941779

    map = create_folium_map(latitude, longitude)
    rotated_image = rotate_image(".\icons8-flugmodus-an-64.png", 0)
    draw_image_on_map(map_object=map, position=[latitude, longitude], rotated_image_path=rotated_image)
    open_folium_map()
