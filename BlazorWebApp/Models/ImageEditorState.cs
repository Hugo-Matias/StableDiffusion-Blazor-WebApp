namespace BlazorWebApp.Models
{
    /// <summary>
    /// Represents the complete state of the image editor, including canvas data,
    /// tool settings, view settings, and history.
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
        
        #endregion
        
        #region Layer Visibility
        
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
            BaseLayerVisible = true;
            DrawingLayerVisible = true;
            DrawingLayerOpacity = 1.0f;
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
        MaskEraser
    }
}
