import numpy as np
import mediapipe as mp

def calculate_angle(a, b, c):
    """
    Calculates the angle between three points (a, b, c) where b is the vertex.
    Returns angle in degrees.
    """
    a = np.array(a) # First
    b = np.array(b) # Mid
    c = np.array(c) # End
    
    # Calculate radians using arctan2 (handles all quadrants correctly)
    radians = np.arctan2(c[1]-b[1], c[0]-b[0]) - np.arctan2(a[1]-b[1], a[0]-b[0])
    angle = np.abs(radians*180.0/np.pi)
    
    # Standardize angle to be within 0-180
    if angle > 180.0:
        angle = 360 - angle
        
    return angle

def are_arms_stretched(mp_pose, landmarks):
    """
    Checks if both arms are:
    1. Visible (high confidence)
    2. Straight (Angle > 160 degrees)
    3. Elevated (Wrists are higher than Shoulders)
    """
    # 1. Extract Landmarks
    l_shoulder = landmarks[mp_pose.PoseLandmark.LEFT_SHOULDER.value]
    l_elbow    = landmarks[mp_pose.PoseLandmark.LEFT_ELBOW.value]
    l_wrist    = landmarks[mp_pose.PoseLandmark.LEFT_WRIST.value]

    r_shoulder = landmarks[mp_pose.PoseLandmark.RIGHT_SHOULDER.value]
    r_elbow    = landmarks[mp_pose.PoseLandmark.RIGHT_ELBOW.value]
    r_wrist    = landmarks[mp_pose.PoseLandmark.RIGHT_WRIST.value]

    # 2. Visibility Check (Robustness against off-screen limbs)
    # MediaPipe visibility is 0.0 to 1.0. We want to be sure the sensor sees them.
    visibility_threshold = 0.5
    if (l_shoulder.visibility < visibility_threshold or
        l_elbow.visibility < visibility_threshold or
        l_wrist.visibility < visibility_threshold or
        r_shoulder.visibility < visibility_threshold or
        r_elbow.visibility < visibility_threshold or
        r_wrist.visibility < visibility_threshold):
        return False

    # 3. Calculate Angles (Robustness against distance/camera zoom)
    # We pass (x, y) coordinates. Note: Y increases downwards in image coordinates.
    left_angle = calculate_angle(
        [l_shoulder.x, l_shoulder.y], 
        [l_elbow.x, l_elbow.y], 
        [l_wrist.x, l_wrist.y]
    )
    
    right_angle = calculate_angle(
        [r_shoulder.x, r_shoulder.y], 
        [r_elbow.x, r_elbow.y], 
        [r_wrist.x, r_wrist.y]
    )

    # 4. Check Logic
    
    # Straightness: Human arms are rarely perfectly 180. >160 is a good threshold.
    is_straight = left_angle > 150 and right_angle > 150
    
    # Elevation: In Computer Vision (OpenCV/MediaPipe), Y=0 is the TOP of the screen.
    # So, for wrists to be "above" shoulders, wrist.y must be LESS THAN shoulder.y
    is_elevated = (l_wrist.y < l_shoulder.y) and (r_wrist.y < r_shoulder.y)

    # Alternatively: Check if wrists are above the NOSE for a "High Stretch"
    nose = landmarks[mp_pose.PoseLandmark.NOSE.value]
    is_high_stretch = (l_wrist.y < nose.y) and (r_wrist.y < nose.y)

    return is_straight and is_elevated and is_high_stretch