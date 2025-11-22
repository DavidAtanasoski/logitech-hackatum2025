import requests
import threading

SERVER_URL = "http://localhost:8085/"

def send_command(endpoint):
    full_url = f"{SERVER_URL}{endpoint}"
    print("Sending command " , full_url)
    requests.post(full_url)