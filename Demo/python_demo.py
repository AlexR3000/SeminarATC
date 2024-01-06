

from flask import Flask, render_template_string, render_template
import threading
import subprocess
import os

app = Flask(__name__)


@app.route('/calculate_new_map')
def calculate_new_map():
    os.system("python demo_map_creator.py")

    with open("./static/map.html") as file:
        return file.read()



@app.route('/map')
def show_map():
    return render_template('index.html')

    

if __name__ == "__main__":
    app.run(debug=True)

        
        


