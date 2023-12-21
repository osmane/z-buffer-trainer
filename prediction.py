import pandas as pd
import numpy as np
import pickle
from tensorflow.keras.models import load_model
import os

# Model ve scaler'ı yükle
model_path = 'my_model.h5'
scaler_path = 'my_scaler.pkl'
model = load_model(model_path)
scaler = pickle.load(open(scaler_path, 'rb'))

# Kontrol dosyaları yolu
control_path = "C:/Users/Osman/My project/TrainingControl"

# Tüm CSV dosyalarını oku ve tahminleri yap
for file in os.listdir(control_path):
    if file.endswith('.csv'):
        df = pd.read_csv(os.path.join(control_path, file))
        
        # "Connected_Vertices" sütununu işle
        df['Connected_Vertices'] = df['Connected_Vertices'].str.split()
        max_len = 30  # 30'a kadar olan bağlantılı köşeleri alıyoruz
        df['Connected_Vertices'] = df['Connected_Vertices'].apply(lambda x: x[:max_len] + ['-1'] * (max_len - len(x)))

        # Yeni sütunlar oluştur
        for i in range(max_len):
            df[f'Vertex_{i}'] = df['Connected_Vertices'].apply(lambda x: x[i])

        # "Connected_Vertices" sütunu artık gereksiz, bu yüzden çıkarabiliriz
        df = df.drop(columns='Connected_Vertices')

        # Girdi verilerini hazırla
        X_control = df.drop(columns=['Is_Visible', 'BehindAMesh', 'Out_Of_Frame'])

        # Verileri ölçeklendir
        X_control = scaler.transform(X_control)

        # Veri setini numpy array olarak dönüştür ve float32 tipine çevir
        X_control = np.array(X_control).astype('float32')

        # Tahminleri yap
        predictions = model.predict(X_control)

        # Tahminleri yeni DataFrame'e ekle
        df_predictions = df.copy()
        df_predictions['Predicted_Is_Visible'] = (predictions[:, 0] > 0.5).astype('int')
        df_predictions['Predicted_BehindAMesh'] = (predictions[:, 1] > 0.5).astype('int')
        df_predictions['Predicted_Out_Of_Frame'] = (predictions[:, 2] > 0.5).astype('int')

        # "Vertex" sütunlarını çıkar
        for i in range(max_len):
            df_predictions = df_predictions.drop(columns=f'Vertex_{i}')
                
        # Değişiklikleri yeni CSV dosyasına kaydet
        df_predictions.to_csv(os.path.join(control_path, 'Predicted_'+file), index=False)
