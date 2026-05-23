using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Geometry;

namespace AutoFixtureDim
{
    public sealed class AnnotationPreview : IDisposable
    {
        private readonly IntegerCollection _viewports = new IntegerCollection();
        private readonly IList<Entity> _entities;
        private bool _visible;

        public AnnotationPreview(IList<Entity> entities)
        {
            _entities = entities;
        }

        public void Show()
        {
            foreach (var entity in _entities)
            {
                TransientManager.CurrentTransientManager.AddTransient(
                    entity,
                    TransientDrawingMode.DirectShortTerm,
                    128,
                    _viewports);
            }

            _visible = true;
        }

        public void Dispose()
        {
            if (_visible)
            {
                foreach (var entity in _entities)
                {
                    TransientManager.CurrentTransientManager.EraseTransient(entity, _viewports);
                }
            }

            foreach (var entity in _entities)
            {
                entity.Dispose();
            }
        }
    }
}
