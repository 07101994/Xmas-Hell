using Microsoft.Xna.Framework;

namespace XmasHell.Entities
{
    public class PlayerBullet : Bullet
    {
        public PlayerBullet(XmasHell game, Vector2 position, float rotation, float speed) :
            base(game, position, rotation, speed)
        {
            Sprite.Color = Color.White * 0.5f;

        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }
    }
}