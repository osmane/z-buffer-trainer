# z-buffer-trainer
This program teaches a machine learning model about the Z-buffer technique present in graphics card drivers and Graphics APIs like DirectX and OpenGL, and runs the trained model upon request.

The majority of this artificial intelligence training project is a Unity project. If training data needs to be generated, Unity must be run initially. In Unity, when the project is executed, the camera can be moved using the arrow keys, and upon pressing the Enter key, it adds a new row to a CSV file with all the vertices and their connected triangles of objects in the scene. The visibility status of these points from the current camera frame is also added to the training data.

The model can be trained using 'Learn.py' with these data. The model can be executed with 'prediction.py'. In my tests, the model was much faster at determining which points were visible compared to the Z-buffer technique. However, I couldn’t implement the surface texturing and other Graphics API and driver tasks due to my lack of knowledge in these areas.

The file 'my_model.h5' contains the model I trained.

Much of the code has been written with the assistance of ChatGPT.


Due to the large number of files and the library being outdated, I did not upload the library folder in its raw form. Instead, I compressed and uploaded it as 'library.part01.rar'..., which you can use by unraring. Alternatively, if you create a new project in Unity and overwrite it with the other file contents, it will likely resolve itself automatically.
