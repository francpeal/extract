from datetime import datetime, timezone
import unittest

from sico_etl.domain.models import Entity, Page
from sico_etl.pipelines import EntityPipeline


class FakeClient:
    def iter_pages(self, entity: Entity, updated_since=None):
        yield Page(
            items=({"articleCode": "A", "active": True},),
            next_cursor=None,
            extracted_at=datetime.now(timezone.utc),
        )


class PipelineTests(unittest.TestCase):
    def test_dry_run_never_requires_repository(self) -> None:
        metrics = EntityPipeline(FakeClient()).run(Entity.ARTICLES, dry_run=True)

        self.assertEqual(metrics.rows_read, 1)
        self.assertEqual(metrics.rows_unchanged, 0)
        self.assertEqual(metrics.rows_inserted, 0)

    def test_write_requires_repository(self) -> None:
        with self.assertRaisesRegex(ValueError, "repository"):
            EntityPipeline(FakeClient()).run(Entity.ARTICLES, dry_run=False)


if __name__ == "__main__":
    unittest.main()
