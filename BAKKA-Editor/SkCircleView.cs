﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using SkiaSharp;
using System.Drawing;
using SkiaSharp.Views.Desktop;
using System.Reflection.PortableExecutable;
using System.IO;

namespace BAKKA_Editor
{
    internal struct SkArcInfo
    {
        public float StartAngle;
        public float ArcLength;
        public SKRect Rect;
        public float NoteScale;
    }

    internal class SkCircleView
    {
        public SizeF PanelSize { get; private set; }
        public SKRect DrawRect { get; private set; }
        public PointF TopCorner { get; private set; }
        public PointF CenterPoint { get; private set; }
        public float Radius { get; private set; }
        public float CurrentMeasure { get; set; }
        /// <summary>
        /// Number of measures in the future that are visible
        /// </summary>
        public float TotalMeasureShowNotes { get; set; } = 0.5f;

        // Pens and Brushes
        public SKPaint BeatPen { get; set; }
        public SKPaint TickMinorPen { get; set; }
        public SKPaint TickMediumPen { get; set; }
        public SKPaint TickMajorPen { get; set; }
        public SKPaint HoldBrush { get; set; } = new SKPaint()
        {
            IsAntialias = true,
            Color = Color.Yellow.ToSKColor().WithAlpha(170),
            Style = SKPaintStyle.Fill
        };
        public SKPaint MaskBrush { get; set; } = new SKPaint()
        {
            IsAntialias = true,
            Color = Color.Black.ToSKColor().WithAlpha(90),
            Style = SKPaintStyle.Fill
        };
        public SKPaint BackgroundBrush { get; set; }
        public SKPaint HighlightPen { get; set; }
        public SKPaint FlairPen { get; set; }
        private int CursorTransparency = 110;
        private int SelectTransparency = 110;
        private int FlairTransparency = 75;

        // Graphics.
        // BufferedGraphics bufGraphics;
        SKCanvas canvas;

        // Mouse information. Public so other GUI elements can be updated with their values.
        public int mouseDownPos = -1;
        public Point mouseDownPt;
        public int lastMousePos = -1;
        public bool rolloverPos = false;
        public bool rolloverNeg = false;

        public SkCircleView(SizeF size)
        {
            Update(size);
        }

        public void SetCanvas(SKCanvas canvas)
        {
            this.canvas = canvas;
        }

        public void Update(SizeF size)
        {
            PanelSize = size;
            float basePenWidth = PanelSize.Width * 4.0f / 600.0f;
            TopCorner = new PointF(basePenWidth * 4, basePenWidth * 4);
            DrawRect = new SKRect(
                TopCorner.X,
                TopCorner.Y,
                PanelSize.Width - basePenWidth * 8,
                PanelSize.Height - basePenWidth * 8);
            Radius = DrawRect.Width / 2.0f;
            CenterPoint = new PointF(TopCorner.X + Radius, TopCorner.Y + Radius);

            // Pens
            BeatPen = new SKPaint()
            {
                IsAntialias = true,
                Color = SKColors.White,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = PanelSize.Width * 1.0f / 600.0f
            };
            TickMinorPen = new SKPaint()
            {
                IsAntialias = true,
                Color = SKColors.Black,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = PanelSize.Width * 2.0f / 600.0f
            };
            TickMediumPen = new SKPaint()
            {
                IsAntialias = true,
                Color = SKColors.Black,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = PanelSize.Width * 4.0f / 600.0f
            };
            TickMajorPen = new SKPaint()
            {
                IsAntialias = true,
                Color = SKColors.Black,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = PanelSize.Width * 7.0f / 600.0f
            };
            HighlightPen = new SKPaint()
            {
                IsAntialias = true,
                Color = Color.LightPink.ToSKColor().WithAlpha((byte)SelectTransparency),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = PanelSize.Width * 8.0f / 600.0f
            };
            FlairPen = new SKPaint()
            {
                IsAntialias = true,
                Color = SKColors.Yellow.WithAlpha((byte)FlairTransparency),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = PanelSize.Width * 8.0f / 600.0f
            };
        }


        private SkArcInfo GetScaledRect(float objectTime)
        {
            SkArcInfo info = new();
            float notescaleInit = 1 - ((objectTime - CurrentMeasure) * (1 / TotalMeasureShowNotes));  // Scale from 0-1
            info.NoteScale = (float)Math.Pow(10.0f, notescaleInit) / 10.0f;
            float scaledRectSize = DrawRect.Width * info.NoteScale;
            float scaledRadius = scaledRectSize / 2.0f;
            info.Rect = SKRect.Create(
                    CenterPoint.X - scaledRadius,
                    CenterPoint.Y - scaledRadius,
                    scaledRectSize,
                    scaledRectSize);
            return info;
        }

        private SkArcInfo GetSkArcInfo(Note note)
        {
            SkArcInfo info = GetScaledRect(note.Measure);
            info.StartAngle = -note.Position * 6;
            info.ArcLength = -note.Size * 6;
            if(info.ArcLength != -360)
            {
                info.StartAngle -= 2;
                info.ArcLength += 4;
            }
            return info;
        }

        // Updates the mouse down position within the circle, and returns the new position.
        public void UpdateMouseDown(float xCen, float yCen, Point mousePt)
        {
            float theta = (float)(Math.Atan2(yCen, xCen) * 180.0f / Math.PI);
            if (theta < 0)
                theta += 360.0f;
            // Left click moves the cursor
            mouseDownPos = (int)(theta / 6.0f);
            mouseDownPt = mousePt;
            lastMousePos = -1;
            rolloverPos = false;
            rolloverNeg = false;
        }

        public void UpdateMouseUp()
        {
            if (mouseDownPos <= -1)
            {
                return;
            }

            // Reset position and point.
            mouseDownPos = -1;
            mouseDownPt = new Point();
        }

        // Updates the mouse position and returns the new position in degrees.
        public int UpdateMouseMove(float xCen, float yCen)
        {
            float thetaCalc = (float)(Math.Atan2(yCen, xCen) * 180.0f / Math.PI);
            if (thetaCalc < 0)
                thetaCalc += 360.0f;
            int theta = (int)(thetaCalc / 6.0f);

            int delta = theta - lastMousePos;
            // Handle rollover
            if (delta == -59)
            {
                if (rolloverNeg)
                    rolloverNeg = false;
                else
                    rolloverPos = true;
            }
            else if (delta == 59)
            {
                if (rolloverPos)
                    rolloverPos = false;
                else
                    rolloverNeg = true;
            }
            lastMousePos = theta;

            return theta;
        }

        private void FillPie(SKPaint paint, SKRect rect, float startAngle, float sweepAngle)
        {
            var path = new SKPath();
            path.FillType = SKPathFillType.EvenOdd;
            path.AddArc(rect, startAngle, sweepAngle);
            canvas.DrawPath(path, paint);
        }

        public void DrawMasks(Chart chart)
        {
            var masks = chart.Notes.Where(
            x => x.Measure <= CurrentMeasure
            && x.IsMask).ToList();
            foreach (var mask in masks)
            {
                if (mask.NoteType == NoteType.MaskAdd)
                {
                    // Check if there's a MaskRemove that counters the MaskAdd (unless the MaskAdd is later)
                    var rem = masks.FirstOrDefault(x => x.NoteType == NoteType.MaskRemove &&
                                                   x.Position == mask.Position && x.Size == mask.Size);
                    if (rem == null || rem.Measure < mask.Measure)
                        FillPie(MaskBrush, DrawRect, -mask.Position * 6.0f, -mask.Size * 6.0f);
                }
                else if (mask.NoteType == NoteType.MaskRemove) // Explicitly draw MaskRemove for edge cases
                {
                    FillPie(BackgroundBrush, DrawRect, -mask.Position * 6.0f, -mask.Size * 6.0f);
                }
            }
        }

        public void DrawCircle()
        {
            // Draw measure circle
            for (float meas = (float)Math.Ceiling(CurrentMeasure); (meas - CurrentMeasure) < TotalMeasureShowNotes; meas += 1.0f)
            {
                var info = GetScaledRect(meas);
                if (info.Rect.Width >= 1)
                {
                    canvas.DrawOval(info.Rect, BeatPen);
                }
            }

            // Draw base circle
            canvas.DrawOval(DrawRect, TickMediumPen);
        }

        public void DrawDegreeLines()
        {
            for (int i = 0; i < 360; i += 6)
            {
                SKPoint startPoint = new SKPoint(
                   (float)(Radius * Math.Cos(Utils.DegToRad(i))) + CenterPoint.X,
                   (float)(Radius * Math.Sin(Utils.DegToRad(i))) + CenterPoint.Y);
                float tickLength = PanelSize.Width * 10.0f / 285.0f;
                float innerRad = Radius - tickLength;
                SKPaint activePen;
                if (i % 90 == 0)
                {
                    innerRad = Radius - (tickLength * 3.5f);
                    activePen = TickMajorPen;
                }
                else if (i % 30 == 0)
                {
                    innerRad = Radius - (tickLength * 2.5f);
                    activePen = TickMediumPen;
                }
                else
                {
                    activePen = TickMinorPen;
                }
                SKPoint endPoint = new SKPoint(
                    (float)(innerRad * Math.Cos(Utils.DegToRad(i))) + CenterPoint.X,
                   (float)(innerRad * Math.Sin(Utils.DegToRad(i))) + CenterPoint.Y);

                canvas.DrawLine(startPoint, endPoint, activePen);
            }
        }

        public void DrawHolds(Chart chart, bool highlightSelectedNote, int selectedNoteIndex)
        {
            SkArcInfo currentInfo = GetScaledRect(CurrentMeasure);
            SkArcInfo endInfo = GetScaledRect(CurrentMeasure + TotalMeasureShowNotes);

            // First, draw holes that start before the viewpoint and have nodes that end after
            List<Note> holdNotes = chart.Notes.Where(
                x => x.Measure < CurrentMeasure
                && x.NextNote != null
                && x.NextNote.Measure > (CurrentMeasure + TotalMeasureShowNotes)
                && x.IsHold).ToList();
            foreach (var note in holdNotes)
            {
                SkArcInfo info = GetSkArcInfo(note);
                SkArcInfo nextInfo = GetSkArcInfo((Note)note.NextNote);
                //GraphicsPath path = new GraphicsPath();
                //path.AddArc(endInfo.Rect, info.StartAngle, info.ArcLength);
                //path.AddArc(currentInfo.Rect, info.StartAngle + info.ArcLength, -info.ArcLength);
                //bufGraphics.Graphics.FillPath(HoldBrush, path);

                float ratio = (currentInfo.Rect.Width - nextInfo.Rect.Width) / (info.Rect.Width - nextInfo.Rect.Width);
                float startNoteAngle = nextInfo.StartAngle;
                float endNoteAngle = info.StartAngle;
                if (nextInfo.StartAngle > info.StartAngle && (Math.Abs(nextInfo.StartAngle - info.StartAngle) > 180))
                {
                    startNoteAngle -= 360;
                }
                else if (info.StartAngle > nextInfo.StartAngle && (Math.Abs(nextInfo.StartAngle - info.StartAngle) > 180))
                {
                    endNoteAngle -= 360;
                }
                float startAngle = ratio * (endNoteAngle - startNoteAngle) + startNoteAngle;
                float endAngle = ratio * ((endNoteAngle - info.ArcLength) - (startNoteAngle - nextInfo.ArcLength)) +
                    (startNoteAngle - nextInfo.ArcLength);
                float arcLength = startAngle - endAngle;

                float ratio2 = (endInfo.Rect.Width - nextInfo.Rect.Width) / (info.Rect.Width - nextInfo.Rect.Width);
                float startNoteAngle2 = nextInfo.StartAngle;
                float endNoteAngle2 = info.StartAngle;
                if (nextInfo.StartAngle > info.StartAngle && (Math.Abs(nextInfo.StartAngle - info.StartAngle) > 180))
                {
                    startNoteAngle2 -= 360;
                }
                else if (info.StartAngle > nextInfo.StartAngle && (Math.Abs(nextInfo.StartAngle - info.StartAngle) > 180))
                {
                    endNoteAngle2 -= 360;
                }
                float startAngle2 = ratio2 * (endNoteAngle2 - startNoteAngle2) + startNoteAngle2;
                float endAngle2 = ratio2 * ((endNoteAngle2 - info.ArcLength) - (startNoteAngle2 - nextInfo.ArcLength)) +
                    (startNoteAngle2 - nextInfo.ArcLength);
                float arcLength2 = startAngle2 - endAngle2;

                var p = new SKPath();
                p.ArcTo(currentInfo.Rect, startAngle, arcLength, true);
                p.ArcTo(endInfo.Rect, startAngle2 + arcLength2, -arcLength2, false);
                canvas.DrawPath(p, HoldBrush);

                /*GraphicsPath path = new GraphicsPath();
                path.AddArc(currentInfo.Rect, startAngle, arcLength);
                path.AddArc(endInfo.Rect, startAngle2 + arcLength2, -arcLength2);
                draw.FillPath(HoldBrush, path);*/
            }

            // Second, draw all the notes on-screen
            holdNotes = chart.Notes.Where(
            x => x.Measure >= CurrentMeasure
               && x.Measure <= (CurrentMeasure + TotalMeasureShowNotes)
               && x.IsHold).ToList();
            foreach (var note in holdNotes)
            {
                SkArcInfo info = GetSkArcInfo(note);

                // If the previous note is off-screen, this case handles that
                if (note.PrevNote != null && note.PrevNote.Measure < CurrentMeasure)
                {
                    SkArcInfo prevInfo = GetSkArcInfo((Note)note.PrevNote);
                    float ratio = (currentInfo.Rect.Width - info.Rect.Width) / (prevInfo.Rect.Width - info.Rect.Width);
                    float startNoteAngle = info.StartAngle;
                    float endNoteAngle = prevInfo.StartAngle;
                    if (info.StartAngle > prevInfo.StartAngle && (Math.Abs(info.StartAngle - prevInfo.StartAngle) > 180))
                    {
                        startNoteAngle -= 360;
                    }
                    else if (prevInfo.StartAngle > info.StartAngle && (Math.Abs(info.StartAngle - prevInfo.StartAngle) > 180))
                    {
                        endNoteAngle -= 360;
                    }
                    float startAngle = ratio * (endNoteAngle - startNoteAngle) + startNoteAngle;
                    float endAngle = ratio * ((endNoteAngle - prevInfo.ArcLength) - (startNoteAngle - info.ArcLength)) +
                        (startNoteAngle - info.ArcLength);
                    float arcLength = startAngle - endAngle;

                    var p = new SKPath();
                    p.ArcTo(info.Rect, info.StartAngle, info.ArcLength, true);
                    p.ArcTo(currentInfo.Rect, startAngle + arcLength, -arcLength, false);
                    canvas.DrawPath(p, HoldBrush);
                }

                // If the next note is on-screen, this case handles that
                if (note.NextNote != null && note.NextNote.Measure <= (CurrentMeasure + TotalMeasureShowNotes))
                {
                    SkArcInfo nextInfo = GetSkArcInfo(note.NextNote);
                    var p = new SKPath();
                    p.ArcTo(info.Rect, info.StartAngle, info.ArcLength, true);
                    p.ArcTo(nextInfo.Rect, nextInfo.StartAngle + nextInfo.ArcLength, -nextInfo.ArcLength, false);
                    canvas.DrawPath(p, HoldBrush);
                }

                // If the next note is off-screen, this case handles that
                if (note.NextNote != null && note.NextNote.Measure > (CurrentMeasure + TotalMeasureShowNotes))
                {
                    SkArcInfo nextInfo = GetSkArcInfo(note.NextNote);
                    float ratio = (endInfo.Rect.Width - nextInfo.Rect.Width) / (info.Rect.Width - nextInfo.Rect.Width);
                    float startNoteAngle = nextInfo.StartAngle;
                    float endNoteAngle = info.StartAngle;
                    if (nextInfo.StartAngle > info.StartAngle && (Math.Abs(nextInfo.StartAngle - info.StartAngle) > 180))
                    {
                        startNoteAngle -= 360;
                    }
                    else if (info.StartAngle > nextInfo.StartAngle && (Math.Abs(nextInfo.StartAngle - info.StartAngle) > 180))
                    {
                        endNoteAngle -= 360;
                    }
                    float startAngle = ratio * (endNoteAngle - startNoteAngle) + startNoteAngle;
                    float endAngle = ratio * ((endNoteAngle - info.ArcLength) - (startNoteAngle - nextInfo.ArcLength)) +
                        (startNoteAngle - nextInfo.ArcLength);
                    float arcLength = startAngle - endAngle;

                    var p = new SKPath();
                    p.ArcTo(endInfo.Rect, startAngle, arcLength, true);
                    p.ArcTo(info.Rect, info.StartAngle + info.ArcLength, -info.ArcLength, false);
                    canvas.DrawPath(p, HoldBrush);
                }

                // Draw note
                if (info.Rect.Width >= 1)
                {
                    // bufGraphics.Graphics.DrawArc(GetPen(note), info.Rect, info.StartAngle, info.ArcLength);
                    var magic = false;
                    canvas.DrawArc(info.Rect, info.StartAngle, info.ArcLength, magic, GetPen(note));

                    // Draw bonus
                    if (note.IsFlair)
                        canvas.DrawArc(info.Rect, info.StartAngle + 2, info.ArcLength - 4, magic, HighlightPen);

                    // Plot highlighted
                    if (highlightSelectedNote)
                    {
                        if (selectedNoteIndex != -1 && note == chart.Notes[selectedNoteIndex])
                        {
                            canvas.DrawArc(info.Rect, info.StartAngle + 2, info.ArcLength - 4, magic, HighlightPen);
                        }
                    }
                }
            }
        }

        public void DrawNotes(Chart chart, bool highlightSelectedNote, int selectedNoteIndex)
        {
            List<Note> drawNotes = chart.Notes.Where(
            x => x.Measure >= CurrentMeasure
            && x.Measure <= (CurrentMeasure + TotalMeasureShowNotes)
            && !x.IsHold && !x.IsMask).ToList();
            foreach (var note in drawNotes)
            {
                SkArcInfo info = GetSkArcInfo(note);

                if (info.Rect.Width >= 1)
                {
                    var magic = false;

                    canvas.DrawArc(info.Rect, info.StartAngle, info.ArcLength, magic, GetPen(note, info.NoteScale));
                    if (note.IsFlair)
                    {
                        canvas.DrawArc(info.Rect, info.StartAngle + 2, info.ArcLength - 4, magic, FlairPen);
                    }
                    // Plot highlighted
                    if (highlightSelectedNote)
                    {
                        if (selectedNoteIndex != -1 && note == chart.Notes[selectedNoteIndex])
                        {
                            canvas.DrawArc(info.Rect, info.StartAngle + 2, info.ArcLength - 4, magic, HighlightPen);
                        }
                    }
                }
            }
        }

        public void DrawCursor(NoteType noteType, float startAngle, float sweepAngle)
        {
            canvas.DrawArc(DrawRect, -(float)startAngle * 6.0f,
                -(float)sweepAngle * 6.0f,
                false,
                GetPen(noteType)
            );
        }

        public SKPaint GetPen(Note note, float noteScale = 1.0f)
        {
            return new SKPaint()
            {
                IsAntialias = true,
                Color = note.Color.ToSKColor(),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = PanelSize.Width * 8.0f * noteScale / 600.0f
            };
        }

        public SKPaint GetPen(NoteType noteType)
        {
            var color = Utils.NoteTypeToColor(noteType);
            return new SKPaint()
            {
                IsAntialias = true,
                Color = new SKColor(color.R, color.G, color.B, (byte)CursorTransparency),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = PanelSize.Width * 24.0f / 600.0f
            };
        }
    }
}
