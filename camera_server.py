import cv2
import dlib
import numpy as np
import time
import mediapipe as mp
from command_sender import send_command
from drowsiness_detector import eye_aspect_ratio
from stretch_detector import are_arms_stretched

# Set to True to get camera window
DEBUG = True


# Stretching State and mediapipe setup
ARMS_STRETCH_LIMIT = 0.5
arms_stretched_start = None
stretch_alarm_sent = False
mp_pose = mp.solutions.pose
pose = mp_pose.Pose(static_image_mode=False, min_detection_confidence=0.5, min_tracking_confidence=0.5)

# EAR State and dlib setup
COUNTER = 0
ALARM_ON = False 
LAST_SLEEPY_SENT_TIME = 0
detector = dlib.get_frontal_face_detector()
try:
    predictor = dlib.shape_predictor("models/shape_predictor_68_face_landmarks.dat")
except RuntimeError:
    print("ERROR: Model not found. Ensure 'models/shape_predictor_68_face_landmarks.dat' exists.")
    exit()

EAR_THRESHOLD = 0.32
EAR_CONSEC_FRAMES = 48 
REQUEST_FREQUENCY_SEC = 0.5
(lStart, lEnd) = (42, 48)
(rStart, rEnd) = (36, 42)


cap = cv2.VideoCapture(0)
if DEBUG: print("[INFO] Starting video stream...")
else: print("[INFO] Running in HEADLESS mode (No GUI). Press Ctrl+C to stop.")

try:
    while True:
        ret, frame = cap.read()
        if not ret: break

        image_rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
        
        current_time = time.time()
        pose_results = pose.process(image_rgb)
        
        if pose_results.pose_landmarks:
            landmarks = pose_results.pose_landmarks.landmark
            
            if are_arms_stretched(mp_pose, landmarks):
                if arms_stretched_start is None:
                    arms_stretched_start = current_time
                
                elapsed = current_time - arms_stretched_start
                
                if elapsed >= ARMS_STRETCH_LIMIT:
                    send_command("/camera_stretching")
                    stretch_alarm_sent = True

                if DEBUG:
                    cv2.putText(frame, f"Stretching: {elapsed:.1f}s", (10, 100), 
                                cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 255, 255), 2)
            else:
                arms_stretched_start = None
                stretch_alarm_sent = False

            if DEBUG:
                mp.solutions.drawing_utils.draw_landmarks(
                    frame, pose_results.pose_landmarks, mp_pose.POSE_CONNECTIONS)

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

            if DEBUG:
                leftEyeHull = cv2.convexHull(leftEye)
                rightEyeHull = cv2.convexHull(rightEye)
                cv2.drawContours(frame, [leftEyeHull], -1, (0, 255, 0), 1)
                cv2.drawContours(frame, [rightEyeHull], -1, (0, 255, 0), 1)

            if ear < EAR_THRESHOLD:
                COUNTER += 1

                if COUNTER >= EAR_CONSEC_FRAMES:
                    if not ALARM_ON:
                        ALARM_ON = True
                        send_command("/camera_sleepy")
                        LAST_SLEEPY_SENT_TIME = current_time 
                    
                    elif (current_time - LAST_SLEEPY_SENT_TIME) >= REQUEST_FREQUENCY_SEC:
                        send_command("/camera_sleepy")
                        LAST_SLEEPY_SENT_TIME = current_time 

                    if DEBUG:
                        cv2.putText(frame, "Drowsiness detected!", (10, 30),
                            cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 0, 255), 2)
            else:
                COUNTER = 0
                if ALARM_ON:
                    ALARM_ON = False
                    send_command("/camera_awake")

            if DEBUG:
                cv2.putText(frame, "EAR: {:.2f}".format(ear), (300, 30),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 0, 255), 2)

        if DEBUG:
            cv2.imshow("Frame", frame)
            if cv2.waitKey(1) == ord("q"):
                break
        
        if not DEBUG and not ret:
            time.sleep(0.01)

except KeyboardInterrupt:
    print("[INFO] Stopping...")

cap.release()
cv2.destroyAllWindows()