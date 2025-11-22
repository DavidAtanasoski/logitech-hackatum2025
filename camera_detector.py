import cv2
import dlib
import numpy as np
from scipy.spatial import distance as dist
import requests
import threading
import time

# --- CONFIGURATION ---
DEBUG = False  # <--- SET THIS TO TRUE TO SEE THE CAMERA WINDOW
EAR_THRESHOLD = 0.32
EAR_CONSEC_FRAMES = 48 
SERVER_URL = "http://127.0.0.1:8085"
REQUEST_FREQUENCY_SEC = 0.5

# Initialize counters and state
COUNTER = 0
ALARM_ON = False 
LAST_SLEEPY_SENT_TIME = 0

# --- HELPER: Send Request Async ---
def send_command(endpoint):
    def _send():
        try:
            full_url = f"{SERVER_URL}{endpoint}"
            if DEBUG: print(f"[NETWORK] Sending POST to {full_url}")
            requests.post(full_url, json={"source": "camera_drowsiness"})
        except Exception as e:
            if DEBUG: print(f"[NETWORK ERROR] Could not connect to Loupedeck: {e}")
    
    # We use a daemon thread so it doesn't block the script from exiting
    t = threading.Thread(target=_send)
    t.daemon = True
    t.start()

# --- FUNCTIONS ---
def eye_aspect_ratio(eye):
    A = dist.euclidean(eye[1], eye[5])
    B = dist.euclidean(eye[2], eye[4])
    C = dist.euclidean(eye[0], eye[3])
    ear = (A + B) / (2.0 * C)
    return ear

# --- SETUP ---
if DEBUG: print("[INFO] Loading facial landmark predictor...")
detector = dlib.get_frontal_face_detector()
try:
    predictor = dlib.shape_predictor("models/shape_predictor_68_face_landmarks.dat")
except RuntimeError:
    print("ERROR: Model not found. Ensure 'models/shape_predictor_68_face_landmarks.dat' exists.")
    exit()

(lStart, lEnd) = (42, 48)
(rStart, rEnd) = (36, 42)

cap = cv2.VideoCapture(0)
if DEBUG: print("[INFO] Starting video stream...")
else: print("[INFO] Running in HEADLESS mode (No GUI). Press Ctrl+C to stop.")

try:
    while True:
        ret, frame = cap.read()
        if not ret: break

        gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
        rects = detector(gray, 0)

        for rect in rects:
            shape = predictor(gray, rect)
            shape_np = np.zeros((68, 2), dtype="int")
            for i in range(0, 68):
                shape_np[i] = (shape.part(i).x, shape.part(i).y)

            leftEye = shape_np[lStart:lEnd]
            rightEye = shape_np[rStart:rEnd]
            leftEAR = eye_aspect_ratio(leftEye)
            rightEAR = eye_aspect_ratio(rightEye)

            ear = (leftEAR + rightEAR) / 2.0
            current_time = time.time()

            # Only calculate convex hulls and draw contours if Debugging
            if DEBUG:
                leftEyeHull = cv2.convexHull(leftEye)
                rightEyeHull = cv2.convexHull(rightEye)
                cv2.drawContours(frame, [leftEyeHull], -1, (0, 255, 0), 1)
                cv2.drawContours(frame, [rightEyeHull], -1, (0, 255, 0), 1)

            # --- DROWSINESS LOGIC (Runs Always) ---
            if ear < EAR_THRESHOLD:
                COUNTER += 1

                if COUNTER >= EAR_CONSEC_FRAMES:
                    # Case 1: Alarm just started
                    if not ALARM_ON:
                        ALARM_ON = True
                        send_command("/camera_sleepy")
                        LAST_SLEEPY_SENT_TIME = current_time 
                    
                    # Case 2: Alarm is continuing, check if we need to resend
                    elif (current_time - LAST_SLEEPY_SENT_TIME) >= REQUEST_FREQUENCY_SEC:
                        send_command("/camera_sleepy")
                        LAST_SLEEPY_SENT_TIME = current_time 

                    # Only draw text if Debugging
                    if DEBUG:
                        cv2.putText(frame, "DROWSINESS ALERT!", (10, 30),
                            cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 0, 255), 2)
                        cv2.putText(frame, "WAKE UP!", (10, 200),
                            cv2.FONT_HERSHEY_SIMPLEX, 1.5, (0, 0, 255), 4)

            else:
                COUNTER = 0
                if ALARM_ON:
                    ALARM_ON = False
                    send_command("/camera_awake")

            # Only draw EAR text if Debugging
            if DEBUG:
                cv2.putText(frame, "EAR: {:.2f}".format(ear), (300, 30),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 0, 255), 2)

        # --- GUI LOGIC (Only if DEBUG is True) ---
        if DEBUG:
            cv2.imshow("Frame", frame)
            if cv2.waitKey(1) == ord("q"):
                break
        
        # If not debugging, we add a tiny sleep to prevent 100% CPU usage loop
        # (though cap.read() usually blocks enough, it's good practice)
        if not DEBUG and not ret:
            time.sleep(0.01)

except KeyboardInterrupt:
    print("[INFO] Stopping...")

# Cleanup
cap.release()
cv2.destroyAllWindows()