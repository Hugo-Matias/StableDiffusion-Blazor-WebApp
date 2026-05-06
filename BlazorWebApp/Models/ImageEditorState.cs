namespace BlazorWebApp.Models
{
    /// <summary>
    /// Represents the complete state of the image editor, including canvas data,
    /// tool settings, view settings, history, and layers.
    /// </summary>
    public class ImageEditorState
    {
        #region Base Image
        
        /// <summary>
        /// Original image data as base64 string (data:image/png;base64,...)
        /// </summary>
        public string? BaseImageData { get; set; }
        
        /// <summary>
        /// Width of the base image in pixels
        /// </summary>
        public int BaseImageWidth { get; set; }
        
        /// <summary>
        /// Height of the base image in pixels
        /// </summary>
        public int BaseImageHeight { get; set; }
        
        /// <summary>
        /// True if editor was started with a blank canvas (no image loaded)
        /// </summary>
        public bool IsBlankCanvas { get; set; }
        
        /// <summary>
        /// Default canvas width for blank canvas mode
        /// </summary>
        public int BlankCanvasWidth { get; set; } = 1024;
        
        /// <summary>
        /// Default canvas height for blank canvas mode
        /// </summary>
        public int BlankCanvasHeight { get; set; } = 1024;
        
        #endregion
        
        #region Canvas State
        
        /// <summary>
        /// Fabric.js JSON serialization of the full canvas state
        /// </summary>
        public string? CanvasJson { get; set; }
        
        /// <summary>
        /// Whether the editor has unsaved changes
        /// </summary>
        public bool IsDirty { get; set; }
        
        #endregion
        
        #region Tool Settings
        
        /// <summary>
        /// Currently selected tool
        /// </summary>
        public ImageEditorTool CurrentTool { get; set; } = ImageEditorTool.Brush;
        
        /// <summary>
        /// Current brush/stroke color in hex format (#RRGGBB)
        /// </summary>
        public string BrushColor { get; set; } = "#FFFFFF";
        
        /// <summary>
        /// Brush size in pixels (1-200)
        /// </summary>
        public int BrushSize { get; set; } = 20;
        
        /// <summary>
        /// Brush opacity (0.0 - 1.0)
        /// </summary>
        public float BrushOpacity { get; set; } = 1.0f;
        
        /// <summary>
        /// Secondary color for mask erasing (usually black)
        /// </summary>
        public string SecondaryColor { get; set; } = "#000000";
        
        #endregion
        
        #region View Settings
        
        /// <summary>
        /// Current zoom level (1.0 = 100%)
        /// </summary>
        public float ZoomLevel { get; set; } = 1.0f;
        
        /// <summary>
        /// Minimum allowed zoom level
        /// </summary>
        public float MinZoom { get; set; } = 0.1f;
        
        /// <summary>
        /// Maximum allowed zoom level
        /// </summary>
        public float MaxZoom { get; set; } = 5.0f;
        
        /// <summary>
        /// Current horizontal pan offset
        /// </summary>
        public float PanX { get; set; }
        
        /// <summary>
        /// Current vertical pan offset
        /// </summary>
        public float PanY { get; set; }
        
        #endregion
        
        #region History
        
        /// <summary>
        /// Stack of canvas states for undo functionality
        /// </summary>
        public List<string> UndoStack { get; set; } = new();
        
        /// <summary>
        /// Stack of canvas states for redo functionality
        /// </summary>
        public List<string> RedoStack { get; set; } = new();
        
        /// <summary>
        /// Maximum number of history states to keep
        /// </summary>
        public int MaxHistorySize { get; set; } = 50;
        
        /// <summary>
        /// Whether undo is available
        /// </summary>
        public bool CanUndo => UndoStack.Count > 0;
        
        /// <summary>
        /// Whether redo is available
        /// </summary>
        public bool CanRedo => RedoStack.Count > 0;
        
        #endregion
        
        #region Mask Layer
        
        /// <summary>
        /// Whether a mask has been created
        /// </summary>
        public bool HasMask { get; set; }
        
        /// <summary>
        /// Mask data as separate base64 image (white = inpaint, black = keep)
        /// </summary>
        public string? MaskData { get; set; }
        
        /// <summary>
        /// Whether the mask layer is visible
        /// </summary>
        public bool MaskVisible { get; set; } = true;
        
        /// <summary>
        /// Opacity of the mask overlay (0.0 - 1.0)
        /// </summary>
        public float MaskOpacity { get; set; } = 0.5f;
        
        /// <summary>
        /// Color used to display the mask overlay
        /// </summary>
        public string MaskOverlayColor { get; set; } = "#FF0000";
        
        /// <summary>
        /// Current mask preview mode
        /// </summary>
        public MaskPreviewMode MaskPreviewMode { get; set; } = MaskPreviewMode.Overlay;
        
        #endregion
        
        #region Selection
        
        /// <summary>
        /// Whether an active selection exists on the canvas
        /// </summary>
        public bool HasSelection { get; set; }

        /// <summary>
        /// Whether an active crop region exists on the canvas
        /// </summary>
        public bool HasCropRegion { get; set; }
        
        /// <summary>
        /// The type of the current selection tool (rect, ellipse, lasso)
        /// </summary>
        public string ActiveSelectionType { get; set; } = "rect";
        
        #endregion
        
        #region Layers
        
        /// <summary>
        /// Maximum number of layers allowed
        /// </summary>
        public const int MaxLayerCount = 20;
        
        /// <summary>
        /// List of layers in the editor (ordered bottom to top)
        /// </summary>
        public List<LayerInfo> Layers { get; set; } = new();
        
        /// <summary>
        /// ID of the currently active/selected layer
        /// </summary>
        public string? ActiveLayerId { get; set; }
        
        /// <summary>
        /// Gets the currently active layer
        /// </summary>
        public LayerInfo? ActiveLayer => Layers.FirstOrDefault(l => l.Id == ActiveLayerId);
        
        /// <summary>
        /// Whether the layer panel is expanded
        /// </summary>
        public bool LayerPanelExpanded { get; set; } = true;
        
        /// <summary>
        /// Creates default layers for a new editor session
        /// </summary>
        public void InitializeDefaultLayers()
        {
            Layers.Clear();
            
            // Base Image layer - always at bottom, locked by default
            var baseLayer = new LayerInfo
            {
                Id = LayerInfo.BaseLayerId,
                Name = "Base Image",
                IsVisible = true,
                IsLocked = true,
                Opacity = 1.0f,
                Order = 0,
                LayerType = LayerType.Base
            };
            Layers.Add(baseLayer);
            
            // Default drawing layer
            var drawingLayer = new LayerInfo
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Drawing Layer",
                IsVisible = true,
                IsLocked = false,
                Opacity = 1.0f,
                Order = 1,
                LayerType = LayerType.Drawing
            };
            Layers.Add(drawingLayer);
            
            // Set drawing layer as active
            ActiveLayerId = drawingLayer.Id;
        }
        
        /// <summary>
        /// Adds a new layer
        /// </summary>
        public LayerInfo? AddLayer(string name, LayerType type = LayerType.Drawing)
        {
            if (Layers.Count >= MaxLayerCount)
                return null;
            
            var maxOrder = Layers.Where(l => l.LayerType != LayerType.Mask).Max(l => l.Order);
            
            var layer = new LayerInfo
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                IsVisible = true,
                IsLocked = false,
                Opacity = 1.0f,
                Order = maxOrder + 1,
                LayerType = type
            };
            
            Layers.Add(layer);
            ActiveLayerId = layer.Id;
            
            return layer;
        }
        
        /// <summary>
        /// Removes a layer by ID
        /// </summary>
        public bool RemoveLayer(string layerId)
        {
            var layer = Layers.FirstOrDefault(l => l.Id == layerId);
            if (layer == null || layer.LayerType == LayerType.Base || layer.LayerType == LayerType.Mask)
                return false;
            
            Layers.Remove(layer);
            
            // If removed layer was active, select another
            if (ActiveLayerId == layerId)
            {
                ActiveLayerId = Layers
                    .Where(l => l.LayerType != LayerType.Base && l.LayerType != LayerType.Mask)
                    .OrderByDescending(l => l.Order)
                    .FirstOrDefault()?.Id;
            }
            
            return true;
        }
        
        /// <summary>
        /// Gets layers ordered for display (top to bottom in UI)
        /// </summary>
        public IEnumerable<LayerInfo> GetLayersForDisplay()
        {
            // Mask layer first (if exists), then others by descending order
            var maskLayer = Layers.FirstOrDefault(l => l.LayerType == LayerType.Mask);
            var otherLayers = Layers
                .Where(l => l.LayerType != LayerType.Mask)
                .OrderByDescending(l => l.Order);
            
            if (maskLayer != null)
                yield return maskLayer;
            
            foreach (var layer in otherLayers)
                yield return layer;
        }
        
        #endregion
        
        #region Layer Visibility (Legacy - kept for compatibility)
        
        /// <summary>
        /// Whether the base image layer is visible
        /// </summary>
        public bool BaseLayerVisible { get; set; } = true;
        
        /// <summary>
        /// Whether the drawing layer is visible
        /// </summary>
        public bool DrawingLayerVisible { get; set; } = true;
        
        /// <summary>
        /// Opacity of the drawing layer (0.0 - 1.0)
        /// </summary>
        public float DrawingLayerOpacity { get; set; } = 1.0f;
        
        #endregion
        
        #region Methods
        
        /// <summary>
        /// Resets the editor state to defaults
        /// </summary>
        public void Reset()
        {
            BaseImageData = null;
            BaseImageWidth = 0;
            BaseImageHeight = 0;
            IsBlankCanvas = false;
            CanvasJson = null;
            IsDirty = false;
            CurrentTool = ImageEditorTool.Brush;
            BrushColor = "#FFFFFF";
            BrushSize = 20;
            BrushOpacity = 1.0f;
            ZoomLevel = 1.0f;
            PanX = 0;
            PanY = 0;
            UndoStack.Clear();
            RedoStack.Clear();
            HasMask = false;
            MaskData = null;
            MaskVisible = true;
            MaskOpacity = 0.5f;
            MaskPreviewMode = MaskPreviewMode.Overlay;
            HasSelection = false;
            HasCropRegion = false;
            ActiveSelectionType = "rect";
            BaseLayerVisible = true;
            DrawingLayerVisible = true;
            DrawingLayerOpacity = 1.0f;
            
            // Initialize default layers
            InitializeDefaultLayers();
        }
        
        /// <summary>
        /// Resets layers to a fresh state with the current image as base
        /// Used by "Flatten & Apply" functionality
        /// </summary>
        public void ResetLayersToBase()
        {
            InitializeDefaultLayers();
            IsDirty = false;
            UndoStack.Clear();
            RedoStack.Clear();
        }
        
        /// <summary>
        /// Pushes current state to undo stack
        /// </summary>
        public void PushUndoState(string canvasJson)
        {
            UndoStack.Add(canvasJson);
            
            // Trim history if exceeds max size
            while (UndoStack.Count > MaxHistorySize)
            {
                UndoStack.RemoveAt(0);
            }
            
            // Clear redo stack when new action is performed
            RedoStack.Clear();
            IsDirty = true;
        }
        
        /// <summary>
        /// Pops state from undo stack for undo operation
        /// </summary>
        public string? PopUndoState(string currentState)
        {
            if (UndoStack.Count == 0) return null;
            
            // Push current state to redo stack
            RedoStack.Add(currentState);
            
            // Pop and return the previous state
            var index = UndoStack.Count - 1;
            var state = UndoStack[index];
            UndoStack.RemoveAt(index);
            
            return state;
        }
        
        /// <summary>
        /// Pops state from redo stack for redo operation
        /// </summary>
        public string? PopRedoState(string currentState)
        {
            if (RedoStack.Count == 0) return null;
            
            // Push current state to undo stack
            UndoStack.Add(currentState);
            
            // Pop and return the redo state
            var index = RedoStack.Count - 1;
            var state = RedoStack[index];
            RedoStack.RemoveAt(index);
            
            return state;
        }
        
        /// <summary>
        /// Gets the effective canvas width (base image or blank canvas)
        /// </summary>
        public int GetCanvasWidth() => IsBlankCanvas ? BlankCanvasWidth : BaseImageWidth;
        
        /// <summary>
        /// Gets the effective canvas height (base image or blank canvas)
        /// </summary>
        public int GetCanvasHeight() => IsBlankCanvas ? BlankCanvasHeight : BaseImageHeight;
        
        #endregion
    }
    
    /// <summary>
    /// Represents a single layer in the image editor
    /// </summary>
    public class LayerInfo
    {
        /// <summary>
        /// Special ID for the base image layer
        /// </summary>
        public const string BaseLayerId = "base-image-layer";
        
        /// <summary>
        /// Special ID for the mask layer
        /// </summary>
        public const string MaskLayerId = "mask-layer";
        
        /// <summary>
        /// Unique identifier for this layer
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Display name of the layer
        /// </summary>
        public string Name { get; set; } = "New Layer";
        
        /// <summary>
        /// Whether this layer is visible
        /// </summary>
        public bool IsVisible { get; set; } = true;
        
        /// <summary>
        /// Whether this layer is locked (prevents editing)
        /// </summary>
        public bool IsLocked { get; set; } = false;
        
        /// <summary>
        /// Opacity of this layer (0.0 - 1.0)
        /// </summary>
        public float Opacity { get; set; } = 1.0f;
        
        /// <summary>
        /// Z-order of this layer (higher = on top)
        /// </summary>
        public int Order { get; set; }
        
        /// <summary>
        /// Type of layer content
        /// </summary>
        public LayerType LayerType { get; set; } = LayerType.Drawing;
        
        /// <summary>
        /// Whether this layer can be deleted
        /// </summary>
        public bool CanDelete => LayerType != LayerType.Base && LayerType != LayerType.Mask;
        
        /// <summary>
        /// Whether this layer can be reordered
        /// </summary>
        public bool CanReorder => LayerType != LayerType.Base && LayerType != LayerType.Mask;
    }
    
    /// <summary>
    /// Type of layer content
    /// </summary>
    public enum LayerType
    {
        /// <summary>
        /// Base image layer (background)
        /// </summary>
        Base,
        
        /// <summary>
        /// Drawing/brush strokes layer
        /// </summary>
        Drawing,
        
        /// <summary>
        /// Imported image layer
        /// </summary>
        Import,
        
        /// <summary>
        /// Inpainting mask layer
        /// </summary>
        Mask
    }
    
    /// <summary>
    /// Available tools in the image editor
    /// </summary>
    public enum ImageEditorTool
    {
        /// <summary>
        /// Freehand brush drawing
        /// </summary>
        Brush,
        
        /// <summary>
        /// Eraser tool
        /// </summary>
        Eraser,
        
        /// <summary>
        /// Flood fill tool
        /// </summary>
        Fill,
        
        /// <summary>
        /// Color picker / eyedropper
        /// </summary>
        ColorPicker,
        
        /// <summary>
        /// Pan/move the canvas view
        /// </summary>
        Pan,
        
        /// <summary>
        /// Mask brush (draws on mask layer)
        /// </summary>
        MaskBrush,
        
        /// <summary>
        /// Mask eraser (erases from mask layer)
        /// </summary>
        MaskEraser,
        
        /// <summary>
        /// Selection tool for imported objects
        /// </summary>
        Select,
        
        /// <summary>
        /// Rectangle area selection for mask creation
        /// </summary>
        SelectRect,
        
        /// <summary>
        /// Ellipse area selection for mask creation
        /// </summary>
        SelectEllipse,
        
        /// <summary>
        /// Freeform lasso selection for mask creation
        /// </summary>
        SelectLasso,

        /// <summary>
        /// Rectangular crop tool for destructive image cropping
        /// </summary>
        Crop
    }
    
    /// <summary>
    /// Mask preview/display modes
    /// </summary>
    public enum MaskPreviewMode
    {
        /// <summary>
        /// Colored overlay with stripe pattern (default)
        /// </summary>
        Overlay,
        
        /// <summary>
        /// Binary black/white preview (what gets sent to API)
        /// </summary>
        Binary,
        
        /// <summary>
        /// Marching ants outline only, no fill
        /// </summary>
        MarchingAnts,
        
        /// <summary>
        /// Black out unmasked areas (shows only what will be inpainted)
        /// </summary>
        Blackout,
        
        /// <summary>
        /// White out masked areas (highlights what will be kept)
        /// </summary>
        Whiteout
    }
}
