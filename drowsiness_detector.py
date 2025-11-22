from scipy.spatial import distance as dist
import dlib

detector = dlib.get_frontal_face_detector()
try:
    predictor = dlib.shape_predictor("models/shape_predictor_68_face_landmarks.dat")
except RuntimeError:
    print("ERROR: Model not found. Ensure 'models/shape_predictor_68_face_landmarks.dat' exists.")
    exit()

def eye_aspect_ratio(eye):
    A = dist.euclidean(eye[1], eye[5])
    B = dist.euclidean(eye[2], eye[4])
    C = dist.euclidean(eye[0], eye[3])
    ear = (A + B) / (2.0 * C)
    return ear

