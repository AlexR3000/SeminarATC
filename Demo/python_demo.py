from flask import Flask, render_template
import os

app = Flask(__name__)

script_directory = os.path.dirname(os.path.abspath(__file__))

static_folder = os.path.join(script_directory, 'static')
html_file_path = os.path.join(static_folder, 'map.html')

map_creator_path = os.path.join(script_directory, 'demo_map_creator.py')

@app.route('/calculate_new_map')
def calculate_new_map():
     # using the "{}" with .format fixes an issue with directory names that include spaces
    command = 'python "{}"'.format(map_creator_path)
    os.system(command)
    os.makedirs(static_folder, exist_ok=True)
    with open(html_file_path, 'r+') as file:
        return file.read()


@app.route('/map')
def show_map():
    return render_template('index.html')

    
if __name__ == "__main__":
    app.run(debug=True)

        
        


