using System;
using UnityEngine;

// 카드 데이터는 Resources/Data/Card/*.json에서 읽어온다. 에셋이 아니라 순수 런타임 데이터다.
// 만드는 쪽은 CardJson.ToCardData(), 꺼내 쓰는 쪽은 CardParser.GetDeck().
[Serializable]
public class BaseCardData
{
    public string Title;
    public string Desc;
    public Sprite Image;
    public int RPS;
    public int moveBlockMe;
    public int moveBlockOps;
    public int moveMe;
    public int moveOps;
    public float probability;
    public CardType cardType;
}

public enum CardType
{
    Good,
    SuperGood,
    Bad,
    SuperBad,
    Joker
}

// RPS는 BattleManager가 +1/-1 산술로 승패를 가리므로 BaseCardData에서는 int로 둔다.
// 다만 JSON에는 손으로 쓰기 좋게 이름으로 적고 CardJson에서 이 enum으로 변환한다.
public enum RPSType
{
    Rock,
    Paper,
    Scissor
}
