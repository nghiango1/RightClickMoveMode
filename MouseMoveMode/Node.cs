using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MouseMoveMode
{
    /**
     * @brief This contain a rectangle that can be print to screen
     */
    class DrawableNode
    {
        public Rectangle box;

        public DrawableNode(Rectangle box)
        {
            this.box = box;
        }

        public DrawableNode(int x, int y, int width, int height)
        {
            this.box = new Rectangle(x, y, width, height);
        }

        public void draw(SpriteBatch b)
        {
            DrawHelper.drawBox(b, this.box, Color.White);
        }

        public void draw(SpriteBatch b, Color color)
        {
            DrawHelper.drawBox(b, this.box, color);
        }

        public override String ToString()
        {
            var x = this.box.X;
            var y = this.box.Y;
            var w = this.box.Width;
            var h = this.box.Height;
            return String.Format("x: {0}, y: {1}, w: {2}, h: {3}", x, y, w, h);
        }
    }
}