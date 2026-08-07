"""학습된 ONNX를 Unity에서 바로 쓸 수 있는 형태로 변환한다.

mlagents가 내보낸 .onnx는 그냥 복사하면 Unity에서 깨진다. 두 가지 처리가 필요하다:

  1. 가중치가 별도 .data 파일로 분리돼 있고 .onnx 안에는 파일명 참조만 들어 있다.
     이름을 바꿔 복사하는 순간 참조가 끊긴다 -> 가중치를 파일 안에 내장해서 저장한다.
  2. 기본 익스포트에는 softmax(카드별 선택 확률)가 출력에 없다.
     EnemyPolicy가 확률 로그를 찍으려면 이걸 출력으로 노출시켜야 한다.

사용법:
    python dev/export_model.py                       # 최신 체크포인트를 자동으로 찾음
    python dev/export_model.py --run uku05
    python dev/export_model.py --step 99982 --out Assets/Models/Ukulele-100k.onnx
"""

import argparse
import glob
import os
import re

import onnx
from onnx import TensorProto, helper

HAND_SLOTS = 6  # CardManager.MaxHandSize와 같아야 한다


def find_checkpoint(run: str, step: int | None) -> str:
    pattern = os.path.join("results", run, "*", "*-[0-9]*.onnx")
    paths = glob.glob(pattern)

    if not paths:
        raise SystemExit(f"체크포인트를 못 찾음: {pattern}")

    if step is not None:
        for p in paths:
            if re.search(rf"-{step}\.onnx$", p):
                return p
        raise SystemExit(f"{step} 스텝 체크포인트가 없음. 있는 것: {sorted(steps_of(paths))}")

    # 스텝 번호가 가장 큰 것 = 가장 많이 학습된 것.
    # 수정 시각으로 고르면 --resume 했을 때 엉뚱한 게 걸린다
    return max(paths, key=step_of)


def step_of(path: str) -> int:
    m = re.search(r"-(\d+)\.onnx$", path)
    return int(m.group(1)) if m else -1


def steps_of(paths) -> list[int]:
    return [step_of(p) for p in paths]


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--run", default="uku05", help="results/ 아래 run-id")
    ap.add_argument("--step", type=int, default=None, help="특정 스텝 체크포인트 (기본: 최신)")
    ap.add_argument("--out", default=None, help="출력 경로 (기본: Assets/Models/<run>-<step>k.onnx)")
    args = ap.parse_args()

    src = find_checkpoint(args.run, args.step)
    step = step_of(src)

    out = args.out or os.path.join("Assets", "Models", f"{args.run}-{step // 1000}k.onnx")
    os.makedirs(os.path.dirname(out), exist_ok=True)

    # load_external_data=True 여야 .data의 가중치를 실제로 읽어들인다
    model = onnx.load(src, load_external_data=True)

    if any(o.name == "softmax" for o in model.graph.output):
        print("softmax 출력이 이미 있음")
    else:
        model.graph.output.append(
            helper.make_tensor_value_info("softmax", TensorProto.FLOAT, ["batch", HAND_SLOTS])
        )
        print("softmax를 출력으로 추가함")

    # save_as_external_data=False 로 가중치를 파일 안에 넣는다
    onnx.save_model(model, out, save_as_external_data=False)
    onnx.checker.check_model(onnx.load(out))

    print(f"{src}  ({step} 스텝)")
    print(f"  -> {out}  ({os.path.getsize(out):,} bytes)")
    print("Unity로 전환하면 자동 임포트됩니다. EnemyPolicy의 Model 칸에 넣으세요.")


if __name__ == "__main__":
    main()
