# Blazor Diffusion
## A Stable Diffusion Front End

Using Automatic1111's api we can improve upon the default Gradio graphical interface and re-design it using a more powerful framework such as Blazor.

Implementing the regular Txt2Img, Img2Img and Upscale modes with some custom extensions already supported, such as ControlNet, Dynamic Prompts, MultiDiffusion or Ultimate Upscale, etc. It also includes a project based gallery and a resource/prompt manager with CivitAI integration for a seamless experience.

Disclaimer: This is a demo project, I didn't have shareability in mind so it's tightly catered to my particular setup. Because of that I decided not to version control the appsettings.json file. Feel free to clone/fork but be aware that this needs some tweaks before being able to run on other systems.

### Projects

Project based gallery system to filter and organize your generated images. The projects can also be further segregated into their own folders for a more granular theme separation (not demonstrated in the video).

https://github.com/Hugo-Matias/StableDiffusion-Blazor-WebApp/assets/20263833/e319c7c4-01bd-4872-bb54-a7aca887b800

### Tags

Prompt writing using tag buttons for a more confortable user experience while also being able to increase/decrease the attention weight of the tokens. The buttons are user customizable by modifying a json file to add both tags and categories.

https://github.com/Hugo-Matias/StableDiffusion-Blazor-WebApp/assets/20263833/1c47c77e-c449-4285-84f0-1f791a53cd3a

### Image Inference and Management

Basic image inference on Txt2Img mode with some of the possible parameters and pre-prompt styles. Also showcasing the image viewer and gallery workflow.

https://github.com/Hugo-Matias/StableDiffusion-Blazor-WebApp/assets/20263833/f446d3e5-5fac-4dde-b149-c66d15d36a04

### Info

Info panel useful to check or reuse previously generated parameters. Notice the deterministic nature of Stable Diffusion where the same settings generate the same image.

https://github.com/Hugo-Matias/StableDiffusion-Blazor-WebApp/assets/20263833/9e940aca-1a74-4a7f-9243-de9f12d9914f

### Img2Img

Img2Img is a powerful mode that allows the user to either draw over an image or mask out a portion of it for rendering (inpainting), both features are demonstrated here. This mode uses several Html Canvas stacked over each other to draw the pointer and manipulate pixel information using JSInterop.

https://github.com/Hugo-Matias/StableDiffusion-Blazor-WebApp/assets/20263833/786e43ae-0f45-4d18-80f0-1bc3409f2283

### Upscale

Simple upscale mode that uses GAN's to quickly upscale an image outside of latent space.

https://github.com/Hugo-Matias/StableDiffusion-Blazor-WebApp/assets/20263833/a5f0e11a-b91d-481e-a0a0-b627d538a53d

### Resources

Civitai.com integration for resources and their example images. Downloads are divided into categories (Checkpoint, Lora, Textual Inversion Embedding, etc.) and can also be further organized into user defined sub-types if necessary. Files are managed by the app according to filesystem settings.

https://github.com/Hugo-Matias/StableDiffusion-Blazor-WebApp/assets/20263833/759f9c8f-51d6-4154-bd4b-a049a93acba6

### Prompts

Prompt templates can also be created and used as styles. Dynamic Prompts is a very useful script to generate new and unexpected prompts.

https://github.com/Hugo-Matias/StableDiffusion-Blazor-WebApp/assets/20263833/7303079c-9af0-4141-8283-b0d3f43c30a2

### Wildcards

Following the previous feature, we finish this demo by displaying the potential of user created wildcards, another way to iterate over random prompts and create new interesting images.

https://github.com/Hugo-Matias/StableDiffusion-Blazor-WebApp/assets/20263833/4b8d8ae8-4889-4e70-92e0-b201e37d2868

