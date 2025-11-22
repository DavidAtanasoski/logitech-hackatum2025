import requests
import threading

SERVER_URL = "http://127.0.0.1:8085"

def send_command(endpoint):
    print("Sending command " , endpoint)
    def _send():
        try:
            full_url = f"{SERVER_URL}{endpoint}"
            requests.post(full_url, json={"source": "camera_drowsiness"})
        except Exception as e:
            t = threading.Thread(target=_send)
            t.daemon = True
            t.start()