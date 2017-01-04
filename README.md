# JStudio
JStudio is a collection of classes for working with the J* tools found in Nintendo's Legend of Zelda: Wind Waker.

Currently this only supports the J3D (model+texture), BCK (bone animation), and BTK (uv animation) formats. This emulates the Game Cube's TExture enVironment (TEV) fairly accurately. Requires a modern-ish graphics card with OpenGL 3 support. Probably explodes horribly on anything less than that.

# TEV Emulation
JStudio's J3D classes emulate the native fixed function pipeline of the GameCube to (mostly) accurately render models as they originally looked on GameCube hardware. Because the GameCube has a fixed function rendering pipeline, these fixed functions were emulated via OpenGL shader generation. 

The J3D classes parse the internal material format to determine the 'default' setup for materials and generates valid GLSL vertex and fragment shaders from it. These are then compiled, and the triangles are placed into batches for performant rendering on PC. Bone skinning is implemented via CPU skinning with caching to again increase performance on animating models. 
