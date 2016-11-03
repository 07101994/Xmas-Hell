using System.Collections.Generic;
using Microsoft.Xna.Framework;
using RandomExtension;
using Xmas_Hell.BulletML;
using Xmas_Hell.Geometry;

namespace Xmas_Hell.Entities.Bosses.XmasBall
{
    class XmasBallBehaviour4 : AbstractBossBehaviour
    {
        public XmasBallBehaviour4(Boss boss) : base(boss)
        {
        }

        public override void Start()
        {
            base.Start();

            var possibleAngle = new List<float>()
            {
                45f,
                135f,
                225f,
                315f
            };

            Boss.Direction = MathHelperExtension.AngleToDirection(
                MathHelper.ToRadians(possibleAngle[Boss.Game.GameManager.Random.Next(possibleAngle.Count - 1)])
            );

            Boss.Speed = 500f;
        }


        public override void Stop()
        {
            base.Stop();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            var currentPosition = Boss.CurrentAnimator.Position;
            var collision = false;
            if (currentPosition.X < Boss.Width() / 2f || currentPosition.X > GameConfig.VirtualResolution.X - Boss.Width() / 2f)
            {
                Boss.Direction.X *= -(float)Boss.Game.GameManager.Random.NextDouble(1f, 1f);
                collision = true;
            }

            if (currentPosition.Y < Boss.Height() / 2f || currentPosition.Y > GameConfig.VirtualResolution.Y - Boss.Height() / 2f)
            {
                Boss.Direction.Y *= -(float)Boss.Game.GameManager.Random.NextDouble(1f, 1f);
                collision = true;
            }

            Boss.CurrentAnimator.Position = new Vector2(
                MathHelper.Clamp(Boss.CurrentAnimator.Position.X, Boss.Width() / 2f,
                    GameConfig.VirtualResolution.X - Boss.Width() / 2f),
                MathHelper.Clamp(Boss.CurrentAnimator.Position.Y, Boss.Height() / 2f,
                    GameConfig.VirtualResolution.Y - Boss.Height() / 2f)
            );

            if (collision)
            {
                Boss.Acceleration.X = MathHelper.Clamp(Boss.Acceleration.X + 0.1f, 0f, 5f);
                Boss.Acceleration.Y = MathHelper.Clamp(Boss.Acceleration.Y + 0.1f, 0f, 5f);

                Boss.Game.Camera.Shake(0.25f, 30f);

                var patternPosition = currentPosition;

                if (currentPosition.X < Boss.Width() / 2f)
                    patternPosition.X -= Boss.Width() / 2f;
                else if (currentPosition.X > GameConfig.VirtualResolution.X - Boss.Width() / 2f)
                    patternPosition.X += Boss.Width() / 2f;
                else if (currentPosition.Y < Boss.Height() / 2f)
                    patternPosition.Y -= Boss.Height() / 2f;
                else if (currentPosition.Y > GameConfig.VirtualResolution.Y - Boss.Height() / 2f)
                    patternPosition.Y += Boss.Height() / 2f;

                Boss.TriggerPattern("XmasBall/pattern4", BulletType.Type2, false, patternPosition);
            }
        }
    }
}