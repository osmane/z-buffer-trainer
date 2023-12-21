import tensorflow as tf
print("TensorFlow version:", tf.__version__)

# GPU kullanılıyorsa True, CPU kullanılıyorsa False döndürür
print("GPU availability:", bool(tf.config.list_physical_devices('GPU')))