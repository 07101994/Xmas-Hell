using System;
using Microsoft.Xna.Framework;
using XmasHell.Extensions;
using XmasHell.BulletML;

namespace XmasHell.Entities.Bosses.XmasBell
{
    class XmasBellBehaviour2 : AbstractBossBehaviour
    {
        private TimeSpan _bulletFrequence;

        public XmasBellBehaviour2(Boss boss) : base(boss)
        {
        }

        public override void Start()
        {
            base.Start();

            Boss.Speed = 500f;
            _bulletFrequence = TimeSpan.Zero;

            Boss.CurrentAnimator.Play("Idle");

            Boss.MoveTo(
                new Vector2(
                    -Boss.Width() - 10f,
                    Boss.Game.GameManager.Random.Next((int)(Boss.Height() / 2f), GameConfig.VirtualResolution.Y - (int)(Boss.Height() / 2f))
                ),
                true
            );

            Boss.Invincible = true;
        }

        public override void Stop()
        {
            base.Stop();
        }

        private void GetNewYRandomPosition()
        {
            var newYPosition = Boss.Game.GameManager.Random.NextFloat(
                Boss.Width() / 2f,
                Boss.Game.ViewportAdapter.VirtualHeight - (Boss.Height() * 1.5f)
            );

            Boss.CurrentAnimator.Position = new Vector2(Boss.CurrentAnimator.Position.X, newYPosition);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Boss.Invincible && Boss.IsOutside)
                Boss.Invincible = false;

            // Go from a side to another side of the screen
            if (!Boss.TargetingPosition)
            {
                if (Boss.CurrentAnimator.Position.X > GameConfig.VirtualResolution.X + Boss.Width())
                {
                    GetNewYRandomPosition();
                    Boss.Direction.X = -1;
                }
                else if (Boss.CurrentAnimator.Position.X < -Boss.Width())
                {
                    GetNewYRandomPosition();
                    Boss.Direction.X = 1;
                }

                if (_bulletFrequence.TotalMilliseconds > 0)
                    _bulletFrequence -= gameTime.ElapsedGameTime;
                else
                {
                    _bulletFrequence = TimeSpan.FromSeconds(0.5f);
                    Boss.Game.GameManager.MoverManager.TriggerPattern("XmasBell/pattern1", BulletType.Type2, false, Boss.ActionPointPosition());
                }
            }
        }
    }
}