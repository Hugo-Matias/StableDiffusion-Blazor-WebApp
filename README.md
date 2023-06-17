# Blazor Diffusion
## A Stable Diffusion Front End

Using Automatic1111's api we can improve upon the default Gradio graphical interface and re-design it using a more powerful framework such as Blazor.

Implementing the regular Txt2Img, Img2Img and Upscale modes with some custom extensions already supported, such as ControlNet, Dynamic Prompts, MultiDiffusion or Ultimate Upscale, etc. It also includes a project based gallery and a resource/prompt manager with CivitAI integration for a seamless experience.

Disclaimer: This is a demo project, I didn't have shareability in mind so it's tightly catered to my particular setup. Because of that I decided not to version control the appsettings.json file. Feel free to clone/fork but be aware that this needs some tweaks before being able to run on other systems.

### Projects

Project based gallery system to filter and organize your generated images. The projects can also be further segregated into their own folders for a more granular theme separation (not demonstrated in the video).

https://github.com/Hugo-Matias/StableDiffusion-Blazor-WebApp/assets/20263833/bbd7bb3b-dbcd-4d98-8998-a3420114b651

### Tags

Prompt writing using tag buttons for a more confortable user experience while also being able to increase/decrease the attention weight of the tokens. The buttons are user customizable by modifying a json file to add both tags and categories.

https://github.com/Hugo-Matias/StableDiffusion-Blazor-WebApp/assets/20263833/a2f67cbb-18f3-4da1-b8a6-e11be951c462

### Image Inference and Management

Basic image inference on Txt2Img mode with some of the possible parameters and pre-prompt styles. Also showcasing the image viewer and gallery workflow.

https://github.com/Hugo-Matias/StableDiffusion-Blazor-WebApp/assets/20263833/a9d14702-7cd6-47e7-84f4-fae90ddd2fdc

### Info

Info panel useful to check or reuse previously generated parameters. Notice the deterministic nature of Stable Diffusion where the same settings generate the same image.

https://github.com/Hugo-Matias/StableDiffusion-Blazor-WebApp/assets/20263833/e019fe0d-b3ef-41ff-a68b-24a35ec409b0

### Img2Img

Img2Img is a powerful mode that allows the user to either draw over an image or mask out a portion of it for rendering (inpainting), both features are demonstrated here. This mode uses several Html Canvas stacked over each other to draw the pointer and manipulate pixel information using JSInterop.

https://github.com/Hugo-Matias/StableDiffusion-Blazor-WebApp/assets/20263833/13c32295-9401-4233-8455-4ab5a9d2345c

### Upscale

Simple upscale mode that uses GAN's to quickly upscale an image outside of latent space.

https://github.com/Hugo-Matias/StableDiffusion-Blazor-WebApp/assets/20263833/0531f045-6516-4f36-a112-0d5190e26daa

### Resources

Civitai.com integration for resources and their example images. Downloads are divided into categories (Checkpoint, Lora, Textual Inversion Embedding, etc.) and can also be further organized into user defined sub-types if necessary. Files are managed by the app according to filesystem settings.

https://github.com/Hugo-Matias/StableDiffusion-Blazor-WebApp/assets/20263833/00aae72a-d2db-4d07-b430-13cd00f95778

### Prompts

Prompt templates can also be created and used as styles. Dynamic Prompts is a very useful script to generate new and unexpected prompts.

https://github.com/Hugo-Matias/StableDiffusion-Blazor-WebApp/assets/20263833/1db342c3-4ef2-4dd7-9b11-3d535004f9ed

### Wildcards

Following the previous feature, we finish this demo by displaying the potential of user created wildcards, another way to iterate over random prompts and create new interesting images.

https://github.com/Hugo-Matias/StableDiffusion-Blazor-WebApp/assets/20263833/c32dc6d4-de46-4f86-8f0a-a8abc203d4c3


