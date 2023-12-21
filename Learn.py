import pandas as pd
import numpy as np
import pickle
from tensorflow.keras.models import Sequential
from tensorflow.keras.layers import Conv1D, MaxPooling1D, Flatten, Dense
from tensorflow.keras.models import load_model
import os
from sklearn.preprocessing import StandardScaler

# Dosya yolu
path = "C:/Users/Osman/My project/LearningData"

# Tüm CSV dosyalarını oku
dataframes = []
for file in os.listdir(path):
    if file.endswith('.csv'):
        df = pd.read_csv(os.path.join(path, file))
        dataframes.append(df)

# Tüm CSV dosyalarını bir DataFrame'de birleştir
data = pd.concat(dataframes)

# "Connected_Vertices" sütununu işle
data['Connected_Vertices'] = data['Connected_Vertices'].str.split()

# En fazla 30 boyutluk bir liste oluştur, eksik yerler -1 ile doldur
max_len = 30
data['Connected_Vertices'] = data['Connected_Vertices'].apply(lambda x: x[:max_len] + ['-1'] * (max_len - len(x)))

# Yeni sütunlar oluştur
for i in range(max_len):
    data[f'Vertex_{i}'] = data['Connected_Vertices'].apply(lambda x: x[i])

# "Connected_Vertices" sütunu artık gereksiz, bu yüzden çıkarabiliriz
data = data.drop(columns='Connected_Vertices')

# Girdi ve çıktı verilerini hazırla
X = data.drop(columns=['Is_Visible', 'BehindAMesh', 'Out_Of_Frame'])
y = data[['Is_Visible', 'BehindAMesh', 'Out_Of_Frame']]

# Verileri ölçeklendir
scaler = StandardScaler()
X = scaler.fit_transform(X)

# Scaler'ı kaydet
pickle.dump(scaler, open('my_scaler.pkl', 'wb'))

# Veri setlerini numpy array olarak dönüştür ve float32 tipine çevir
X = np.array(X).astype('float32')
y = np.array(y).astype('float32')

# Modeli oluştur veya varolan modeli yükle
model_path = 'my_model.h5'
if os.path.exists(model_path):
    model = load_model(model_path)
else:
    model = Sequential()
    model.add(Conv1D(filters=64, kernel_size=2, activation='relu', input_shape=(X.shape[1], 1)))
    model.add(MaxPooling1D(pool_size=2))
    model.add(Flatten())
    model.add(Dense(50, activation='relu'))
    model.add(Dense(3, activation='sigmoid'))  # son katmanda 'sigmoid' aktivasyon fonksiyonu kullan
    model.compile(optimizer='adam', loss='binary_crossentropy')  # 'binary_crossentropy' kayıp fonksiyonu kullan

# Modeli eğit
model.fit(X, y, epochs=10, batch_size=32, verbose=1)

# Modeli kaydet
model.save(model_path)